using BCOBCA.BLL.Common;
using Xunit;

namespace BCOBCA.Tests;

public sealed class WalletHistoryFilterTests
{
    [Fact]
    public void TodayUsesVietnamDayAcrossUtcBoundary()
    {
        var range = WalletHistoryFilters.Range("today", DateTimeOffset.Parse("2026-11-08T18:00:00Z"));
        Assert.Equal(DateTimeOffset.Parse("2026-11-08T17:00:00Z"), range.Start);
        Assert.Equal(DateTimeOffset.Parse("2026-11-09T17:00:00Z"), range.End);
    }

    [Fact]
    public void SearchAcceptsDisplayedReferencePrefixAndRejectsOversizedInput()
    {
        Assert.Equal("dh102893", WalletHistoryFilters.Search(" #DH102893 "));
        Assert.Throws<BusinessException>(() => WalletHistoryFilters.Search(new string('a', 201)));
        Assert.Throws<BusinessException>(() => WalletHistoryFilters.Range("invalid", DateTimeOffset.UtcNow));
        Assert.Null(WalletHistoryFilters.Range("all", DateTimeOffset.UtcNow).Start);
    }
}
