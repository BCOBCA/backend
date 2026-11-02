using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Common;

public static class UserNotifications
{
    public static async Task AddAsync(ICommerceStore store, Guid userId, string type, string title, string body, string targetType, Guid targetId, CancellationToken ct)
    {
        var accountId = await store.Set<User>().IgnoreQueryFilters().Where(x => x.Id == userId).Select(x => x.AccountId).SingleAsync(ct);
        store.Set<Notification>().Add(new Notification { RecipientAccountId = accountId, Type = type, Title = title, Body = body, TargetType = targetType, TargetId = targetId, CreatedAt = DateTimeOffset.UtcNow });
    }
}
