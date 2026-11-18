using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed record PushDeviceInput(string Token, string Platform);
public sealed record LiveNotificationsDto(Guid? Head, IReadOnlyList<NotificationDto> Items);
public sealed class LiveNotificationService(ICommerceStore store, IDataProtectionProvider protection)
{
    public async Task RegisterAsync(Guid accountId, Guid sessionFamilyId, PushDeviceInput input, bool enabled, CancellationToken ct)
    {
        if (input.Platform is not ("android" or "ios") || input.Token is null || input.Token.Length > 256
            || !Regex.IsMatch(input.Token, @"^(ExpoPushToken|ExponentPushToken)\[[A-Za-z0-9_-]+\]$")) throw new BusinessException("Thiết bị nhận thông báo không hợp lệ", 400);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.Token)));
        await using var tx = await store.BeginTransactionAsync(ct);
        var device = await store.Set<DeviceToken>().SingleOrDefaultAsync(d => d.TokenHash == hash, ct);
        if (!enabled)
        {
            if (device?.AccountId == accountId) { device.Enabled = false; device.ProtectedToken = null; }
        }
        else
        {
            if (device is null) { device = new DeviceToken { TokenHash = hash, CreatedAt = DateTimeOffset.UtcNow }; store.Set<DeviceToken>().Add(device); }
            device.AccountId = accountId; device.SessionFamilyId = sessionFamilyId; device.Platform = input.Platform; device.Enabled = true;
            device.ProtectedToken = protection.CreateProtector("BCOBCA.PushToken.v1").Protect(input.Token);
        }
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<LiveNotificationsDto> WaitAsync(Guid accountId, Guid? after, CancellationToken ct)
    {
        // Replay recent notifications, rather than relying on insert identity/commit ordering.
        // Clients deduplicate IDs and refresh the durable paginated inbox on reconnect.
        for (var i = 0; i < 11; i++)
        {
            var items = await store.Set<Notification>().AsNoTracking().Where(n => n.RecipientAccountId == accountId)
                .OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id).Take(100)
                .Select(n => new NotificationDto(n.Id, n.Title, n.Body, n.TargetType, n.TargetId, n.ReadAt, n.CreatedAt)).ToListAsync(ct);
            var head = items.FirstOrDefault()?.Id;
            if (after is null || head != after || i == 10) return new(head, items);
            await Task.Delay(1000, ct);
        }
        throw new InvalidOperationException();
    }
}
