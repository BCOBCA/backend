namespace BCOBCA.BLL.Common;

public static class CommerceRules
{
    private static readonly TimeZoneInfo Vietnam = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    public static DateOnly VietnamDate(DateTimeOffset utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, Vietnam).DateTime);

    public static decimal Discount(decimal subtotal, string type, decimal value, decimal? cap)
    {
        if (subtotal < 0 || value < 0 || cap < 0) throw new BusinessException("Giá trị giảm giá không hợp lệ");
        var discount = type switch
        {
            "PERCENT" => subtotal * value / 100,
            "FIXED" => value,
            _ => throw new BusinessException("Loại voucher không hợp lệ")
        };
        return decimal.Round(Math.Clamp(discount, 0, Math.Min(subtotal, cap ?? subtotal)), 2, MidpointRounding.AwayFromZero);
    }

    public static bool CanTransition(string from, string to) => (from, to) switch
    {
        ("PENDING", "CONFIRMED" or "CANCELLED") => true,
        ("CONFIRMED", "READY" or "CANCELLED") => true,
        ("READY", "SHIPPING" or "CANCELLED") => true,
        ("SHIPPING", "COMPLETED") => true,
        _ => false
    };

    public static decimal[] AllocateDiscount(IReadOnlyList<decimal> totals, decimal discount)
    {
        var sum = totals.Sum();
        if (discount < 0 || discount > sum || decimal.Round(discount, 2) != discount || totals.Any(x => x < 0 || decimal.Round(x, 2) != x))
            throw new BusinessException("Số tiền phân bổ không hợp lệ");
        var allocated = new decimal[totals.Count];
        if (discount == 0) return allocated;
        var exact = totals.Select(x => discount * x / sum).ToArray();
        for (var i = 0; i < allocated.Length; i++) allocated[i] = decimal.Floor(exact[i] * 100) / 100;
        var remaining = discount - allocated.Sum();
        // Largest remainder in cents keeps every line nonnegative and preserves the total.
        foreach (var index in Enumerable.Range(0, totals.Count).OrderByDescending(i => exact[i] - allocated[i]).ThenBy(i => i))
        {
            if (remaining == 0) break;
            if (allocated[index] + 0.01m > totals[index]) continue;
            allocated[index] += 0.01m;
            remaining -= 0.01m;
        }
        if (remaining != 0) throw new InvalidOperationException("Discount allocation did not balance.");
        return allocated;
    }
}
