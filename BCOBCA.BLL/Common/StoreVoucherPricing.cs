using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Entities;

namespace BCOBCA.BLL.Common;

public sealed record CheckoutVoucherContext(Voucher Voucher, IReadOnlyList<VoucherTarget> Targets, int TotalUses, int UserUses, bool Owned);
public sealed record AppliedCheckoutVoucher(Voucher Voucher, CheckoutVoucherDto Option);
public sealed record StoreVoucherSelection(IReadOnlyList<CheckoutVoucherDto> Options, IReadOnlyList<AppliedCheckoutVoucher> Applied, IReadOnlyDictionary<Guid, decimal> ShippingDiscounts);

public static class StoreVoucherPricing
{
    private static string Scope(Voucher v) => v.StoreId?.ToString() ?? (v.DiscountType == "SHIPPING" ? "shipping" : "platform");
    private static CheckoutVoucherDto? Best(IEnumerable<CheckoutVoucherDto> options) => options.Where(x => x.IsEligible).OrderByDescending(x => x.DiscountAmount).ThenBy(x => x.ExpiresAt).ThenBy(x => x.Code, StringComparer.Ordinal).FirstOrDefault();

    public static StoreVoucherSelection Apply(IReadOnlyList<OrderItem> lines, IReadOnlyList<CheckoutVoucherContext> contexts, IReadOnlyDictionary<Guid, Guid> categories,
        IReadOnlyDictionary<Guid, decimal> shippingFees, int level, IReadOnlyList<string> codes, bool automatic, bool requireOwned, DateTimeOffset now)
    {
        if (lines.Count == 0 || lines.Any(x => x.StoreId is null || !shippingFees.ContainsKey(x.StoreId.Value)) || shippingFees.Values.Any(x => x < 0)) throw new BusinessException("Chưa có phí vận chuyển cho từng cửa hàng", 409);
        var requested = codes.Select(code => contexts.SingleOrDefault(x => x.Voucher.Code == code) ?? throw new BusinessException("Voucher chưa được thu thập hoặc không khả dụng", 409)).ToArray();
        if (requested.GroupBy(x => Scope(x.Voucher)).Any(g => g.Count() > 1)) throw new BusinessException("Mỗi cửa hàng chỉ dùng một mã, cùng một mã sàn và một mã vận chuyển", 400);
        var applied = new List<AppliedCheckoutVoucher>();
        var options = new List<CheckoutVoucherDto>();
        var shippingDiscounts = shippingFees.ToDictionary(x => x.Key, _ => 0m);
        bool Matches(OrderItem line, CheckoutVoucherContext context) => CheckoutVoucherRules.Matches(line.ProductId, line.BrandId, categories.GetValueOrDefault(line.ProductId), context.Targets);
        CheckoutVoucherDto Evaluate(CheckoutVoucherContext context, IEnumerable<OrderItem> scope, decimal fee)
        {
            var input = scope.Select(x => new CheckoutVoucherLine(x.ProductId, x.BrandId, categories.GetValueOrDefault(x.ProductId), x.UnitPrice * x.Quantity)).ToArray();
            return CheckoutVoucherRules.Evaluate(context.Voucher, context.Targets, input, fee, level, context.TotalUses, context.UserUses, context.Owned, requireOwned, now) with { StoreId = context.Voucher.StoreId };
        }
        void AddMerchandise(CheckoutVoucherContext context, CheckoutVoucherDto option, bool oneProduct)
        {
            if (!option.IsEligible) throw new BusinessException(option.Reason ?? "Voucher không khả dụng", 409);
            var eligible = lines.Where(x => Matches(x, context) && (context.Voucher.StoreId is null || x.StoreId == context.Voucher.StoreId)).OrderByDescending(x => x.TotalPrice).ThenBy(x => x.ProductId).ThenBy(x => x.VariantId).ToArray();
            if (oneProduct) eligible = [eligible[0]];
            var totals = eligible.Select(x => x.TotalPrice).ToArray();
            var precision = decimal.Truncate(option.DiscountAmount) == option.DiscountAmount && totals.All(x => decimal.Truncate(x) == x) ? 0 : 2;
            var amounts = CommerceRules.AllocateDiscount(totals, option.DiscountAmount, precision);
            for (var i = 0; i < eligible.Length; i++) { eligible[i].DiscountAmount += amounts[i]; eligible[i].TotalPrice -= amounts[i]; }
            applied.Add(new(context.Voucher, option));
        }
        var shopContexts = contexts.Where(x => x.Voucher.StoreId is not null).ToArray();
        var shopOptions = shopContexts.Select(context => {
            var option = Evaluate(context, lines.Where(x => x.StoreId == context.Voucher.StoreId), 0) with { AppliedStoreId = context.Voucher.StoreId };
            return context.Voucher.DiscountType == "SHIPPING" ? option with { IsEligible = false, Reason = "Voucher cửa hàng chỉ áp dụng cho tiền hàng", DiscountAmount = 0 } : option;
        }).ToArray();
        options.AddRange(shopOptions);
        var selectedShopOptions = automatic ? shopOptions.GroupBy(x => x.StoreId).Select(g => Best(g)).Where(x => x is not null).Select(x => x!).ToArray() : shopOptions.Where(x => codes.Contains(x.Code)).ToArray();
        foreach (var option in selectedShopOptions) AddMerchandise(shopContexts.Single(x => x.Voucher.Code == option.Code), option, false);

        var platformContexts = contexts.Where(x => x.Voucher.StoreId is null).ToArray();
        var platformOptions = platformContexts.Select(context => {
            var eligible = lines.Where(x => Matches(x, context)).OrderByDescending(x => x.TotalPrice).ThenBy(x => x.ProductId).ThenBy(x => x.VariantId).ToArray();
            var eligibleStores = eligible.Select(x => x.StoreId!.Value).Distinct().ToArray();
            var option = Evaluate(context, lines, eligibleStores.Sum(id => shippingFees[id]));
            if (!option.IsEligible || context.Voucher.DiscountType == "SHIPPING") return option;
            var largest = eligible.FirstOrDefault();
            var remaining = eligible.Sum(x => x.TotalPrice);
            var discount = Math.Min(largest?.TotalPrice ?? 0, CommerceRules.Discount(remaining, context.Voucher.DiscountType, context.Voucher.DiscountValue, context.Voucher.MaxDiscount));
            return option with { DiscountAmount = discount, AppliedStoreId = largest?.StoreId, IsEligible = discount > 0, Reason = discount > 0 ? null : "Sản phẩm đã được giảm hết giá trị" };
        }).ToArray();
        options.AddRange(platformOptions);
        var selectedPlatformOptions = automatic ? platformOptions.GroupBy(x => x.DiscountType == "SHIPPING").Select(g => Best(g)).Where(x => x is not null).Select(x => x!).ToArray() : platformOptions.Where(x => codes.Contains(x.Code)).ToArray();
        foreach (var option in selectedPlatformOptions)
        {
            var context = platformContexts.Single(x => x.Voucher.Code == option.Code);
            if (!option.IsEligible) throw new BusinessException(option.Reason ?? "Voucher không khả dụng", 409);
            if (option.DiscountType != "SHIPPING") { AddMerchandise(context, option, true); continue; }
            var storeIds = lines.Where(x => Matches(x, context)).Select(x => x.StoreId!.Value).Distinct().OrderBy(x => x).ToArray();
            var fees = storeIds.Select(id => shippingFees[id]).ToArray();
            var precision = decimal.Truncate(option.DiscountAmount) == option.DiscountAmount && fees.All(x => decimal.Truncate(x) == x) ? 0 : 2;
            var discounts = CommerceRules.AllocateDiscount(fees, option.DiscountAmount, precision);
            for (var i = 0; i < storeIds.Length; i++) shippingDiscounts[storeIds[i]] = discounts[i];
            applied.Add(new(context.Voucher, option));
        }
        var ownedCodes = contexts.Where(x => x.Owned).Select(x => x.Voucher.Code).ToHashSet(StringComparer.Ordinal);
        return new(options.Where(x => ownedCodes.Contains(x.Code)).ToArray(), applied, shippingDiscounts);
    }
}
