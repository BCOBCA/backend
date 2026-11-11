using BCOBCA.DAL.Entities;

namespace BCOBCA.BLL.Common;

public sealed record StoreOrderAmounts(Guid StoreId, decimal Subtotal, decimal DiscountAmount, decimal ShippingFee, decimal ShippingDiscountAmount)
{
    public decimal TotalAmount => Subtotal - DiscountAmount + ShippingFee - ShippingDiscountAmount;
}

public static class StoreSplitRules
{
    public static IReadOnlyList<StoreOrderAmounts> Summarize(IReadOnlyList<OrderItem> lines, IReadOnlyDictionary<Guid, decimal> shippingFees, IReadOnlyDictionary<Guid, decimal> shippingDiscounts)
    {
        if (lines.Count == 0 || lines.Any(x => x.StoreId is null)) throw new BusinessException("Thông tin cửa hàng không hợp lệ", 409);
        return lines.GroupBy(x => x.StoreId!.Value).OrderBy(x => x.Key).Select(group => {
            if (!shippingFees.TryGetValue(group.Key, out var fee) || fee < 0) throw new BusinessException("Chưa có phí giao hàng cho cửa hàng", 409);
            var discount = shippingDiscounts.GetValueOrDefault(group.Key);
            if (discount < 0 || discount > fee) throw new BusinessException("Giảm phí vận chuyển không hợp lệ", 409);
            return new StoreOrderAmounts(group.Key, group.Sum(x => x.UnitPrice * x.Quantity), group.Sum(x => x.DiscountAmount), fee, discount);
        }).ToArray();
    }
}
