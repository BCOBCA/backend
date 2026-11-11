using BCOBCA.BLL.Common;
using Xunit;
namespace BCOBCA.Tests;

public sealed class LeaderboardPeriodTests
{
    [Theory]
    [InlineData("week", "2026-11-08T17:00:00Z", "2026-11-15T17:00:00Z")]
    [InlineData("month", "2026-10-31T17:00:00Z", "2026-11-30T17:00:00Z")]
    [InlineData("quarter", "2026-09-30T17:00:00Z", "2026-12-31T17:00:00Z")]
    public void UsesVietnamCalendarBoundaries(string period, string start, string end)
    {
        var range = LeaderboardPeriods.Range(period, DateTimeOffset.Parse("2026-11-11T08:00:00Z"));
        Assert.Equal(DateTimeOffset.Parse(start), range.Start);
        Assert.Equal(DateTimeOffset.Parse(end), range.End);
    }
    [Fact]
    public void AllHasNoBoundaryAndUnknownPeriodsAreRejected()
    {
        var now = DateTimeOffset.Parse("2026-12-31T18:00:00Z");
        Assert.Null(LeaderboardPeriods.Range("all", now).Start);
        Assert.Equal(DateTimeOffset.Parse("2026-12-31T17:00:00Z"), LeaderboardPeriods.Range("quarter", now).Start);
        Assert.Equal(400, Assert.Throws<BusinessException>(() => LeaderboardPeriods.Range("yesterday", now)).StatusCode);
    }
}
