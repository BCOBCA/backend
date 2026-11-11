using BCOBCA.BLL.Common;
using BCOBCA.DAL.Entities;
using Xunit;

namespace BCOBCA.Tests;

public sealed class StoreSplitRulesTests
{
    private static OrderItem Line(Guid store, decimal subtotal, decimal discount = 0) => new()
    {
        StoreId = store, Quantity = 1, UnitPrice = subtotal, DiscountAmount = discount, TotalPrice = subtotal - discount
    };

    [Fact]
    public void SummarizesAlreadyPricedStoresWithoutChangingAmounts()
    {
        var stores = Enumerable.Range(1, 3).Select(i => Guid.Parse($"00000000-0000-0000-0000-{i:000000000000}")).ToArray();
        var amounts = CommerceRules.AllocateDiscount([100000m, 200000m, 300000m], 50000m, 0);
        Assert.Equal(new[] { 8333m, 16667m, 25000m }, amounts);
        var lines = stores.Select((id, i) => Line(id, (i + 1) * 100000m, amounts[i])).ToArray();
        var result = StoreSplitRules.Summarize(lines, new Dictionary<Guid, decimal> { [stores[0]] = 10000m, [stores[1]] = 10000m, [stores[2]] = 10000m }, new Dictionary<Guid, decimal> { [stores[0]] = 6667m, [stores[1]] = 6667m, [stores[2]] = 6666m });
        Assert.Equal(3, result.Count);
        Assert.Equal(50000m, result.Sum(x => x.DiscountAmount));
        Assert.Equal(30000m, result.Sum(x => x.ShippingFee));
        Assert.Equal(20000m, result.Sum(x => x.ShippingDiscountAmount));
        Assert.Equal(560000m, result.Sum(x => x.TotalAmount));
        Assert.Equal(amounts, result.Select(x => x.DiscountAmount));
        Assert.All(result, x => Assert.InRange(x.ShippingDiscountAmount, 0m, x.ShippingFee));
        Assert.All(result, x => Assert.Equal(decimal.Truncate(x.TotalAmount), x.TotalAmount));
    }

    [Fact]
    public void SellerTargetedDiscountStaysOnItsOriginalStoreAndVariantsStayTogether()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var result = StoreSplitRules.Summarize([Line(a, 100m, 20m), Line(b, 200m), Line(a, 50m, 10m)], new Dictionary<Guid, decimal> { [a] = 0, [b] = 0 }, new Dictionary<Guid, decimal>());
        Assert.Equal(2, result.Count);
        Assert.Equal(30m, result.Single(x => x.StoreId == a).DiscountAmount);
        Assert.Equal(0m, result.Single(x => x.StoreId == b).DiscountAmount);
        Assert.Equal(120m, result.Single(x => x.StoreId == a).TotalAmount);
    }

    [Fact]
    public void ShippingCanExceedMerchandiseAndZeroPriceStoresRemainValid()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var result = StoreSplitRules.Summarize([Line(a, 0m), Line(b, 1m)], new Dictionary<Guid, decimal> { [a] = 15000m, [b] = 15000m }, new Dictionary<Guid, decimal> { [a] = 15000m, [b] = 15000m });
        Assert.Equal(30000m, result.Sum(x => x.ShippingFee));
        Assert.Equal(1m, result.Sum(x => x.TotalAmount));
        var free = StoreSplitRules.Summarize([Line(a, 0m), Line(b, 0m)], new Dictionary<Guid, decimal> { [a] = 0.01m, [b] = 0m }, new Dictionary<Guid, decimal>());
        Assert.Equal(0.01m, free.Sum(x => x.ShippingFee));
        Assert.All(free, x => Assert.True(x.TotalAmount >= 0));
    }

    [Fact]
    public void RejectsMissingStoreAndInvalidShippingDiscount()
    {
        var a = Guid.NewGuid();
        Assert.Throws<BusinessException>(() => StoreSplitRules.Summarize([new OrderItem()], new Dictionary<Guid, decimal>(), new Dictionary<Guid, decimal>()));
        Assert.Throws<BusinessException>(() => StoreSplitRules.Summarize([Line(a, 1m)], new Dictionary<Guid, decimal> { [a] = 10m }, new Dictionary<Guid, decimal> { [a] = 11m }));
    }
}
