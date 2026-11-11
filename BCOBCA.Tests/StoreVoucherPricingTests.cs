using BCOBCA.BLL.Common;
using BCOBCA.DAL.Entities;
using Xunit;

namespace BCOBCA.Tests;

public sealed class StoreVoucherPricingTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static OrderItem Line(Guid store, decimal amount) => new() { StoreId = store, ProductId = Guid.NewGuid(), VariantId = Guid.NewGuid(), Quantity = 1, UnitPrice = amount, TotalPrice = amount };
    private static CheckoutVoucherContext Coupon(string code, decimal value, Guid? store = null, decimal minimum = 0, string type = "FIXED") => new(new Voucher { Code = code, Name = code, StoreId = store, DiscountType = type, DiscountValue = value, MinOrderAmount = minimum, PerUserLimit = 1, StartsAt = Now.AddDays(-1), ExpiresAt = Now.AddDays(1), Status = "ACTIVE" }, [], 0, 0, true);

    [Fact]
    public void PlatformMinimumUsesWholeCheckoutButDiscountHitsOnlyLargestProduct()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var lines = new[] { Line(a, 100000), Line(b, 120000), Line(b, 150000) };
        var platform = Coupon("PLATFORM50", 50000, minimum: 300000);
        var result = StoreVoucherPricing.Apply(lines, [platform], new Dictionary<Guid, Guid>(), new Dictionary<Guid, decimal> { [a] = 20000, [b] = 30000 }, 1, ["PLATFORM50"], false, true, Now);
        Assert.Equal(0m, lines[0].DiscountAmount); Assert.Equal(0m, lines[1].DiscountAmount); Assert.Equal(50000m, lines[2].DiscountAmount);
        Assert.Equal(b, Assert.Single(result.Applied).Option.AppliedStoreId);
    }

    [Fact]
    public void ShopVouchersStackWithPlatformAndShippingWithoutCrossingStores()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var lines = new[] { Line(a, 100000), Line(b, 200000) };
        var contexts = new[] { Coupon("SHOP-A", 10000, a), Coupon("SHOP-B", 20000, b), Coupon("PLATFORM", 50000, minimum: 300000), Coupon("SHIP", 20000, type: "SHIPPING") };
        var result = StoreVoucherPricing.Apply(lines, contexts, new Dictionary<Guid, Guid>(), new Dictionary<Guid, decimal> { [a] = 17000, [b] = 25000 }, 1, [], true, true, Now);
        Assert.Equal(10000m, lines[0].DiscountAmount); Assert.Equal(70000m, lines[1].DiscountAmount);
        Assert.Equal(4, result.Applied.Count);
        Assert.Equal(20000m, result.ShippingDiscounts.Values.Sum());
        Assert.All(result.ShippingDiscounts, x => Assert.InRange(x.Value, 0m, x.Key == a ? 17000m : 25000m));
    }

    [Fact]
    public void PlatformDiscountRespectsTargetsAndCapsAtTheOneProductRemainingAmount()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var lines = new[] { Line(a, 10000), Line(b, 500000) };
        var coupon = Coupon("TARGET", 50000) with { Targets = [new VoucherTarget { ProductId = lines[0].ProductId }] };
        var result = StoreVoucherPricing.Apply(lines, [coupon], new Dictionary<Guid, Guid>(), new Dictionary<Guid, decimal> { [a] = 10000, [b] = 20000 }, 1, ["TARGET"], false, true, Now);
        Assert.Equal(10000m, Assert.Single(result.Applied).Option.DiscountAmount);
        Assert.Equal(0m, lines[1].DiscountAmount); Assert.Equal(0m, lines[0].TotalPrice);
    }

    [Fact]
    public void RejectsTwoCodesForSameShopAndPreservesExplicitNoVoucherChoice()
    {
        var a = Guid.NewGuid(); var contexts = new[] { Coupon("A", 10000, a), Coupon("B", 20000, a) };
        Assert.Throws<BusinessException>(() => StoreVoucherPricing.Apply([Line(a, 100000)], contexts, new Dictionary<Guid, Guid>(), new Dictionary<Guid, decimal> { [a] = 20000 }, 1, ["A", "B"], false, true, Now));
        Assert.Empty(StoreVoucherPricing.Apply([Line(a, 100000)], contexts, new Dictionary<Guid, Guid>(), new Dictionary<Guid, decimal> { [a] = 20000 }, 1, [], false, true, Now).Applied);
    }
}
