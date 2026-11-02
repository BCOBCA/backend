using BCOBCA.BLL.Common;
using Xunit;

namespace BCOBCA.Tests;

public sealed class CommerceRulesTests
{
    [Fact]
    public void CheckInUsesVietnamDayAcrossUtcBoundary()
    {
        var instant = DateTimeOffset.Parse("2026-11-02T18:30:00Z");
        Assert.Equal(new DateOnly(2026, 11, 3), CommerceRules.VietnamDate(instant));
    }

    [Theory]
    [InlineData(100000, 20, 15000, 15000)]
    [InlineData(100000, 20, null, 20000)]
    [InlineData(100000, 150, null, 100000)]
    public void PercentageDiscountIsCapped(decimal subtotal, decimal value, int? cap, decimal expected)
        => Assert.Equal(expected, CommerceRules.Discount(subtotal, "PERCENT", value, cap));

    [Fact]
    public void FixedDiscountNeverMakesTotalNegative()
        => Assert.Equal(100m, CommerceRules.Discount(100, "FIXED", 500, null));

    [Fact]
    public void UnknownDiscountTypeIsRejected()
        => Assert.Throws<BusinessException>(() => CommerceRules.Discount(100, "UNKNOWN", 1, null));

    [Fact]
    public void AllocatingTinyAmountsNeverOverDiscountsLastLine()
    {
        var totals = new[] { 0.01m, 0.01m, 0.01m, 0.01m };
        var shares = CommerceRules.AllocateDiscount(totals, 0.03m);
        Assert.Equal(0.03m, shares.Sum());
        for (var i = 0; i < shares.Length; i++) Assert.InRange(shares[i], 0, totals[i]);
    }

    [Fact]
    public void AllocationsPreserveMoneyForEveryDiscountUpToFullSubtotal()
    {
        var totals = new[] { 0.01m, 0.17m, 1.03m, 2.98m };
        for (var cents = 0; cents <= 419; cents++)
        {
            var discount = cents / 100m;
            var shares = CommerceRules.AllocateDiscount(totals, discount);
            Assert.Equal(discount, shares.Sum());
            for (var i = 0; i < shares.Length; i++) Assert.InRange(shares[i], 0, totals[i]);
        }
    }

    [Theory]
    [InlineData("PENDING", "CONFIRMED", true)]
    [InlineData("PENDING", "COMPLETED", false)]
    [InlineData("COMPLETED", "CANCELLED", false)]
    [InlineData("SHIPPING", "COMPLETED", true)]
    public void OrderTransitionsAreExplicit(string from, string to, bool allowed)
        => Assert.Equal(allowed, CommerceRules.CanTransition(from, to));
}
