using BCOBCA.BLL.Common;
using BCOBCA.DAL.Entities;
using Xunit;

namespace BCOBCA.Tests;

public sealed class CheckoutVoucherRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);
    private static Voucher Voucher(string code, string type, decimal value) => new() { Code = code, Name = code, DiscountType = type, DiscountValue = value, PerUserLimit = 1, Status = "ACTIVE", StartsAt = Now.AddDays(-1), ExpiresAt = Now.AddDays(1) };
    private static readonly CheckoutVoucherLine[] Lines = [new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500000)];

    [Fact]
    public void ChoosesActualSavingsWithCapsAndOnePerCategory()
    {
        var percent = Voucher("PERCENT", "PERCENT", 50); percent.MaxDiscount = 20000;
        var fixedVoucher = Voucher("FIXED", "FIXED", 50000);
        var ship = Voucher("SHIP", "SHIPPING", 50000);
        var options = new[] { percent, fixedVoucher, ship }.Select(v => CheckoutVoucherRules.Evaluate(v, [], Lines, 20000, 1, 0, 0, true, true, Now)).ToArray();
        Assert.Equal(new[] { "FIXED", "SHIP" }, CheckoutVoucherRules.BestCodes(options));
        Assert.Equal(20000, options[2].DiscountAmount);
    }

    [Fact]
    public void RejectsUnownedExpiredExhaustedAndWrongLevelVouchers()
    {
        var voucher = Voucher("TEST", "FIXED", 50000);
        Assert.False(CheckoutVoucherRules.Evaluate(voucher, [], Lines, 20000, 1, 0, 0, false, true, Now).IsEligible);
        Assert.True(CheckoutVoucherRules.Evaluate(voucher, [], Lines, 20000, 1, 0, 0, false, false, Now).IsEligible); // Legacy public free code.
        voucher.ExpiresAt = Now;
        Assert.False(CheckoutVoucherRules.Evaluate(voucher, [], Lines, 20000, 1, 0, 0, true, true, Now).IsEligible);
        voucher.ExpiresAt = Now.AddDays(1); voucher.UsageLimit = 1;
        Assert.False(CheckoutVoucherRules.Evaluate(voucher, [], Lines, 20000, 1, 1, 0, true, true, Now).IsEligible);
        Assert.False(CheckoutVoucherRules.Evaluate(voucher, [], Lines, 20000, 1, 0, 1, true, true, Now).IsEligible);
        voucher.MinLevel = 2;
        Assert.False(CheckoutVoucherRules.Evaluate(voucher, [], Lines, 20000, 1, 0, 0, true, true, Now).IsEligible);
    }

    [Fact]
    public void UsesOnlyTargetedMerchandiseForMinimumAndDiscount()
    {
        var voucher = Voucher("TARGET", "PERCENT", 10); voucher.MinOrderAmount = 400000;
        var targets = new[] { new VoucherTarget { ProductId = Lines[0].ProductId } };
        var allLines = Lines.Append(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1000000)).ToArray();
        Assert.Equal(50000, CheckoutVoucherRules.Evaluate(voucher, targets, allLines, 0, 1, 0, 0, true, true, Now).DiscountAmount);
        voucher.MinOrderAmount = 600000;
        Assert.False(CheckoutVoucherRules.Evaluate(voucher, targets, allLines, 0, 1, 0, 0, true, true, Now).IsEligible);
    }

    [Fact]
    public void NoShippingSavingsWhenShippingIsAlreadyFree()
    {
        var option = CheckoutVoucherRules.Evaluate(Voucher("SHIP", "SHIPPING", 30000), [], Lines, 0, 1, 0, 0, true, true, Now);
        Assert.False(option.IsEligible); Assert.Empty(CheckoutVoucherRules.BestCodes([option]));
    }

    [Fact]
    public void RejectsInvalidSelections()
    {
        foreach (var codes in new[] { new[] { "A", "B", "C" }, new[] { "A", "a" }, new[] { "" } })
            Assert.Throws<BusinessException>(() => CheckoutVoucherRules.NormalizeCodes(null, codes, false));
    }

    [Fact]
    public void RejectsMixedLegacyAndNewContract() => Assert.Throws<BusinessException>(() => CheckoutVoucherRules.NormalizeCodes("OLD", ["NEW"], false));

    [Fact]
    public void LegacyOrderHashOmitsTheNewNullField()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new BCOBCA.BLL.DTOs.CheckoutRequest(Guid.Empty));
        Assert.DoesNotContain("VoucherCodes", json);
        Assert.DoesNotContain("AffiliateVisitToken", json);
    }
}
