using System.Security.Cryptography;
using System.Text;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Common;

public static class AffiliateSources
{
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static async Task CaptureAsync(ICommerceStore store, Order order, IReadOnlyList<OrderItem> lines, string? token, CancellationToken ct)
    {
        if (!store.HasTransaction) throw new InvalidOperationException("Attribution requires the checkout transaction.");
        if (token is { Length: 96 })
        {
            var hash = Hash(token);
            var visit = await store.Set<AffiliateVisit>().SingleOrDefaultAsync(x => x.TokenHash == hash && x.ExpiresAt > DateTimeOffset.UtcNow, ct);
            if (visit is not null && (visit.UserId is null || visit.UserId == order.UserId))
            {
                var link = await store.Set<AffiliateLink>().SingleAsync(x => x.Id == visit.AffiliateLinkId, ct);
                var profile = await store.Set<AffiliateProfile>().SingleOrDefaultAsync(x => x.Id == link.AffiliateProfileId && x.Status == "ACTIVE" && x.UserId != order.UserId, ct);
                if (profile is not null && link.ProductId is not null && lines.Any(x => x.ProductId == link.ProductId)
                    && await (from user in store.Set<User>() join account in store.Set<Account>() on user.AccountId equals account.Id where user.Id == profile.UserId && account.Status == 1 select user.Id).AnyAsync(ct))
                {
                    visit.UserId = order.UserId;
                    store.Set<AffiliateAttribution>().Add(new AffiliateAttribution { OrderId = order.Id, AffiliateProfileId = profile.Id, AffiliateLinkId = link.Id, CreatedAt = DateTimeOffset.UtcNow });
                    return;
                }
            }
        }
        // Preserve legacy one-level member referral attribution when no valid product source applies.
        var referrer = await store.Set<User>().Where(x => x.Id == order.UserId).Select(x => x.ReferredByUserId).SingleAsync(ct);
        if (referrer is null || referrer == order.UserId) return;
        var legacy = await store.Set<AffiliateProfile>().SingleOrDefaultAsync(x => x.UserId == referrer && x.Status == "ACTIVE", ct);
        if (legacy is not null) store.Set<AffiliateAttribution>().Add(new AffiliateAttribution { OrderId = order.Id, AffiliateProfileId = legacy.Id, CreatedAt = DateTimeOffset.UtcNow });
    }
}
