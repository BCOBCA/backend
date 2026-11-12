namespace BCOBCA.BLL.Common;

public static class WalletHistoryFilters
{
    public static (DateTimeOffset? Start, DateTimeOffset? End) Range(string period, DateTimeOffset now)
    {
        if (period == "today")
        {
            var start = new DateTimeOffset(now.ToOffset(TimeSpan.FromHours(7)).Date, TimeSpan.FromHours(7));
            return (start.ToUniversalTime(), start.AddDays(1).ToUniversalTime());
        }
        if (period is not ("all" or "week" or "month" or "quarter")) throw new BusinessException("Khoảng thời gian giao dịch không hợp lệ");
        return LeaderboardPeriods.Range(period, now);
    }

    public static string Search(string? value)
    {
        if (value?.Length > 200) throw new BusinessException("Từ khóa tìm giao dịch quá dài");
        return (value ?? "").Trim().TrimStart('#').ToLowerInvariant();
    }
}
