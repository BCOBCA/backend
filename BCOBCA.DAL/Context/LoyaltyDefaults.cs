using BCOBCA.DAL.Entities;

namespace BCOBCA.DAL.Context;

public static class LoyaltyDefaults
{
    public static MembershipTier[] MembershipLevels() => new[] { 0, 100, 300, 600, 1000, 1600, 2500, 4000, 6000, 10000 }
        .Select((points, index) => new MembershipTier
        {
            Id = Guid.Parse(index == 0 ? "a704b6f3-5664-4428-93d1-668522026001" : $"a704b6f3-5664-4428-93d1-668522026{100 + index:000}"),
            Name = $"Lv.{index + 1}", MinPoints = points, MinSpend = 0, SortOrder = index, IsActive = true, Benefits = "{\"items\":[]}"
        }).ToArray();

    public static AffiliateRank[] AffiliateRanks()
    {
        var names = new[] { "Đồng", "Bạc", "Vàng", "Bạch kim", "Kim cương" };
        var counts = new[] { 0, 10, 30, 70, 150 };
        var rates = new[] { 0.02m, 0.03m, 0.05m, 0.07m, 0.10m };
        return names.Select((name, index) => new AffiliateRank
        {
            Id = Guid.Parse($"a704b6f3-5664-4428-93d1-668522026{201 + index:000}"),
            Rank = index + 1, Name = name, MinSuccessfulReferrals = counts[index], CommissionRate = rates[index]
        }).ToArray();
    }
}
