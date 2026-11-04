using System.Text.Json;
using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Common;

public static class LoyaltyQueries
{
    public static async Task<IReadOnlyList<MembershipLevelDto>> MembershipLevelsAsync(ICommerceStore store, CancellationToken ct)
    {
        var tiers = await store.Set<MembershipTier>().AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ToListAsync(ct);
        var levels = tiers.Select(x => new MembershipLevelDto(x.SortOrder + 1, x.MinPoints, ReadBenefits(x.Benefits))).ToArray();
        MembershipLevelRules.Validate(levels);
        return levels;
    }

    private static IReadOnlyList<string> ReadBenefits(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("items", out var items) ? items.Deserialize<string[]>() ?? Array.Empty<string>() : Array.Empty<string>();
    }

    public static async Task RefreshMembershipAsync(ICommerceStore store, UserMembership membership, CancellationToken ct)
    {
        var level = MembershipLevelRules.Calculate(membership.AvailablePoints, await MembershipLevelsAsync(store, ct)).Level;
        membership.TierId = await store.Set<MembershipTier>().Where(x => x.IsActive && x.SortOrder == level - 1).Select(x => x.Id).SingleAsync(ct);
    }

    public static async Task<IReadOnlyList<AffiliateRankDto>> AffiliateRanksAsync(ICommerceStore store, CancellationToken ct)
    {
        var ranks = await store.Set<AffiliateRank>().AsNoTracking().OrderBy(x => x.Rank)
            .Select(x => new AffiliateRankDto(x.Rank, x.Name, x.MinSuccessfulReferrals, x.CommissionRate)).ToArrayAsync(ct);
        AffiliateRankRules.Validate(ranks);
        return ranks;
    }

    public static Task<int> SuccessfulReferralsAsync(ICommerceStore store, Guid userId, CancellationToken ct)
        => store.Set<AffiliateReferral>().CountAsync(referral => referral.ReferrerUserId == userId
            && store.Set<Order>().Any(order => order.UserId == referral.ReferredUserId && order.Status == "COMPLETED" && order.PaymentStatus == "PAID"), ct);

    public static async Task<AffiliateRankProgressDto> AffiliateRankAsync(ICommerceStore store, Guid userId, CancellationToken ct)
        => AffiliateRankRules.Calculate(await SuccessfulReferralsAsync(store, userId, ct), await AffiliateRanksAsync(store, ct));
}
