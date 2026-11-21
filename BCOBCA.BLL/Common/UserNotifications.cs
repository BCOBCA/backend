using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Common;

public static class UserNotifications
{
    public static async Task AddAsync(ICommerceStore store, Guid userId, string type, string title, string body, string targetType, Guid targetId, CancellationToken ct, string? sellerBody = null)
    {
        var accountId = await store.Set<User>().IgnoreQueryFilters().Where(x => x.Id == userId).Select(x => x.AccountId).SingleAsync(ct);
        await AddAccountAsync(store, accountId, type, title, body, targetType, targetId, ct);
        if (targetType == "Order")
        {
            var owners = await (from line in store.Set<OrderItem>() join shop in store.Set<Store>() on line.StoreId equals shop.Id
                where line.OrderId == targetId && shop.OwnerAccountId != accountId select shop.OwnerAccountId).Distinct().ToArrayAsync(ct);
            foreach (var owner in owners) await AddAccountAsync(store, owner, type, title, sellerBody ?? body, targetType, targetId, ct);
        }
    }

    public static async Task AddAccountAsync(ICommerceStore store, Guid accountId, string type, string title, string body, string targetType, Guid targetId, CancellationToken ct)
    {
        var notification = new Notification { RecipientAccountId = accountId, Type = type, Title = title, Body = body, TargetType = targetType, TargetId = targetId, CreatedAt = DateTimeOffset.UtcNow };
        store.Set<Notification>().Add(notification);
        var devices = await store.Set<DeviceToken>().Where(d => d.AccountId == accountId && d.Enabled && d.ProtectedToken != null).Select(d => d.Id).ToArrayAsync(ct);
        foreach (var device in devices) store.Set<PushDelivery>().Add(new PushDelivery { NotificationId = notification.Id, DeviceTokenId = device, NextAttemptAt = notification.CreatedAt });
    }
}
