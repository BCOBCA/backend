using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Context;
using Xunit;

namespace BCOBCA.Tests;

public sealed class AffiliateRankTests
{
    private static IReadOnlyList<AffiliateRankDto> Ranks => LoyaltyDefaults.AffiliateRanks()
        .Select(x => new AffiliateRankDto(x.Rank, x.Name, x.MinSuccessfulReferrals, x.CommissionRate)).ToArray();

    [Theory]
    [InlineData(0, 1, "0.02")] [InlineData(9, 1, "0.02")]
    [InlineData(10, 2, "0.03")] [InlineData(29, 2, "0.03")]
    [InlineData(30, 3, "0.05")] [InlineData(59, 3, "0.05")]
    [InlineData(60, 4, "0.07")] [InlineData(99, 4, "0.07")]
    [InlineData(100, 5, "0.10")] [InlineData(1000, 5, "0.10")]
    public void SuccessfulReferralCountSelectsTheFinalRate(int referrals, int rank, string rate)
    {
        var result = AffiliateRankRules.Calculate(referrals, Ranks);
        Assert.Equal(rank, result.Rank);
        Assert.Equal(decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture), result.CommissionRate);
    }

    [Fact]
    public void ReportsNextRankAndMaximumRank()
    {
        var current = AffiliateRankRules.Calculate(9, Ranks);
        Assert.Equal("Bạc", current.NextRank);
        Assert.Equal(1, current.ReferralsToNextRank);
        Assert.Null(AffiliateRankRules.Calculate(100, Ranks).NextRank);
    }

    [Fact]
    public void RejectsInvalidAdminConfigurations()
    {
        Assert.Throws<BusinessException>(() => AffiliateRankRules.Validate(Ranks.Take(4).ToArray()));
        var ranks = Ranks.ToArray(); ranks[4] = ranks[4] with { CommissionRate = 1.1m };
        Assert.Throws<BusinessException>(() => AffiliateRankRules.Validate(ranks));
        ranks = Ranks.ToArray(); ranks[1] = ranks[1] with { MinSuccessfulReferrals = 0 };
        Assert.Throws<BusinessException>(() => AffiliateRankRules.Validate(ranks));
        ranks = Ranks.ToArray(); ranks[1] = ranks[1] with { CommissionRate = 0.01m };
        Assert.Throws<BusinessException>(() => AffiliateRankRules.Validate(ranks));
    }
}
