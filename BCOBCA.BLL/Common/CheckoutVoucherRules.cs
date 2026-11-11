using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Entities;

namespace BCOBCA.BLL.Common;

public sealed record CheckoutVoucherLine(Guid ProductId, Guid BrandId, Guid CategoryId, decimal Subtotal);

public static class CheckoutVoucherRules
{
    public static string[] NormalizeCodes(string? legacyCode, IReadOnlyList<string>? codes, bool autoSelect)
    {
        if (legacyCode?.Length > 50 || codes?.Count > 102 || !string.IsNullOrWhiteSpace(legacyCode) && (codes is not null || autoSelect) || autoSelect && codes?.Count > 0)
            throw new BusinessException("Danh sách voucher không hợp lệ", 400);
        var result = codes?.ToArray() ?? (string.IsNullOrWhiteSpace(legacyCode) ? [] : [legacyCode]);
        if (result.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 50)) throw new BusinessException("Mã voucher không hợp lệ", 400);
        result = result.Select(x => x.Trim().ToUpperInvariant()).ToArray();
        if (result.Distinct(StringComparer.Ordinal).Count() != result.Length) throw new BusinessException("Không được chọn trùng voucher", 400);
        return result;
    }

    public static bool Matches(Guid productId, Guid brandId, Guid categoryId, IReadOnlyList<VoucherTarget> targets)
        => targets.Count == 0 || targets.Any(t => (t.ProductId == null || t.ProductId == productId) && (t.BrandId == null || t.BrandId == brandId) && (t.CategoryId == null || t.CategoryId == categoryId));

    public static CheckoutVoucherDto Evaluate(Voucher voucher, IReadOnlyList<VoucherTarget> targets, IReadOnlyList<CheckoutVoucherLine> lines, decimal shippingFee, int level, int totalUses, int userUses, bool owned, bool requireOwned, DateTimeOffset now)
    {
        string? reason = null;
        var eligibleTotal = lines.Where(x => Matches(x.ProductId, x.BrandId, x.CategoryId, targets)).Sum(x => x.Subtotal);
        if (voucher.Status != "ACTIVE" && !(voucher.Status == "MEMBER_OFFER" && (voucher.PointCost > 0 || voucher.MinLevel > 1 || voucher.DiscountType == "SHIPPING"))) reason = "Voucher không khả dụng";
        else if (voucher.StartsAt > now) reason = "Voucher chưa đến thời gian sử dụng";
        else if (voucher.ExpiresAt <= now) reason = "Voucher đã hết hạn";
        else if ((requireOwned || voucher.PointCost > 0) && !owned) reason = "Voucher chưa được thu thập hoặc đã dùng hết";
        else if (level < voucher.MinLevel) reason = $"Ưu đãi dành cho thành viên từ Lv.{voucher.MinLevel}";
        else if (voucher.UsageLimit is not null && totalUses >= voucher.UsageLimit || userUses >= voucher.PerUserLimit) reason = "Voucher đã hết lượt sử dụng";
        else if (eligibleTotal <= 0 || eligibleTotal < voucher.MinOrderAmount) reason = "Đơn hàng chưa đủ điều kiện voucher";
        else if (voucher.DiscountType == "SHIPPING" && shippingFee <= 0) reason = "Đơn hàng đã được miễn phí vận chuyển";
        else if (voucher.DiscountType is not ("SHIPPING" or "FIXED" or "PERCENT") || voucher.DiscountValue < 0 || voucher.MaxDiscount < 0) reason = "Giá trị giảm giá không hợp lệ";
        var discount = reason is null ? CommerceRules.Discount(voucher.DiscountType == "SHIPPING" ? shippingFee : eligibleTotal, voucher.DiscountType == "SHIPPING" ? "FIXED" : voucher.DiscountType, voucher.DiscountValue, voucher.MaxDiscount) : 0;
        if (reason is null && discount <= 0) reason = "Voucher không tạo ra khoản giảm cho đơn hàng này";
        return new(voucher.Id, voucher.Code, voucher.Name, voucher.Description, voucher.DiscountType, voucher.DiscountValue, voucher.MaxDiscount, voucher.MinOrderAmount, voucher.ExpiresAt, discount, reason is null, reason);
    }

    public static string[] BestCodes(IEnumerable<CheckoutVoucherDto> options)
        => options.Where(x => x.IsEligible && x.DiscountAmount > 0).GroupBy(x => x.DiscountType == "SHIPPING")
            .OrderBy(group => group.Key).Select(group => group.OrderByDescending(x => x.DiscountAmount).ThenBy(x => x.ExpiresAt).ThenBy(x => x.Code, StringComparer.Ordinal).First().Code).ToArray();
}
