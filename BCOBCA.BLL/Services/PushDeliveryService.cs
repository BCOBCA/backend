using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BCOBCA.BLL.Services;

public sealed class PushDeliveryService(ICommerceStore store, HttpClient client, IDataProtectionProvider protection, IConfiguration config)
{
    public async Task<bool> DeliverOneAsync(CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct);
        var rows = await store.Set<PushDelivery>().FromSqlRaw("""
            SELECT * FROM "PushDeliveries" WHERE "Status" IN ('PENDING','RECEIPT') AND "NextAttemptAt" <= now()
            ORDER BY "NextAttemptAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        var delivery = rows.FirstOrDefault();
        if (delivery is null) return false;
        var notification = await store.Set<Notification>().AsNoTracking().SingleAsync(n => n.Id == delivery.NotificationId, ct);
        var device = await store.Set<DeviceToken>().SingleAsync(d => d.Id == delivery.DeviceTokenId, ct);
        var now = DateTimeOffset.UtcNow;
        var activeSession = await store.Set<RefreshToken>().AnyAsync(t => t.AccountId == device.AccountId && t.FamilyId == device.SessionFamilyId && t.RevokedAt == null && t.ExpiresAt > now, ct)
            && await store.Set<Account>().AnyAsync(a => a.Id == device.AccountId && a.Status == 1, ct);
        if (!device.Enabled || device.ProtectedToken is null || device.AccountId != notification.RecipientAccountId || !activeSession)
            delivery.Status = "REVOKED";
        else
        {
            delivery.Attempts++;
            try
            {
                var receipt = delivery.Status == "RECEIPT";
                using var request = new HttpRequestMessage(HttpMethod.Post, receipt ? "https://exp.host/--/api/v2/push/getReceipts" : "https://exp.host/--/api/v2/push/send");
                if (config["Push:ExpoAccessToken"] is { Length: > 0 } token) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (receipt) request.Content = JsonContent.Create(new { ids = new[] { delivery.TicketId } });
                else request.Content = JsonContent.Create(new {
                    to = protection.CreateProtector("BCOBCA.PushToken.v1").Unprotect(device.ProtectedToken),
                    title = notification.Title, body = notification.Body, sound = "default", channelId = "orders", ttl = 86400,
                    data = new { notificationId = notification.Id, targetType = notification.TargetType, targetId = notification.TargetId },
                });
                using var response = await client.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (!body.TryGetProperty("data", out var data)) throw new JsonException();
                JsonElement result;
                if (receipt)
                {
                    if (!data.TryGetProperty(delivery.TicketId!, out result))
                    { delivery.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(5); result = default; }
                }
                else result = data.ValueKind == JsonValueKind.Array ? data[0] : data;
                if (result.ValueKind == JsonValueKind.Object)
                {
                    if (result.GetProperty("status").GetString() == "ok")
                    {
                        if (receipt) delivery.Status = "SENT";
                        else
                        {
                            delivery.TicketId = result.GetProperty("id").GetString();
                            if (string.IsNullOrWhiteSpace(delivery.TicketId)) throw new JsonException();
                            delivery.Status = "RECEIPT"; delivery.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(15);
                        }
                    }
                    else
                    {
                        var reason = result.TryGetProperty("details", out var details) && details.TryGetProperty("error", out var error) ? error.GetString() : null;
                        if (reason == "DeviceNotRegistered") { device.Enabled = false; device.ProtectedToken = null; }
                        if (reason is "DeviceNotRegistered" or "MessageTooBig" or "MismatchSenderId" or "InvalidCredentials") delivery.Status = "FAILED";
                        else throw new HttpRequestException();
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or System.Security.Cryptography.CryptographicException || ex is TaskCanceledException && !ct.IsCancellationRequested)
            { delivery.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Min(delivery.Attempts, 6)))); }
            if (delivery.Attempts >= 100 && delivery.Status is "PENDING" or "RECEIPT") delivery.Status = "FAILED";
        }
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return true;
    }
}
