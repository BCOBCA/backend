namespace BCOBCA.BLL.Common;

public static class LeaderboardPeriods
{
    public static (DateTimeOffset? Start, DateTimeOffset? End) Range(string period, DateTimeOffset now)
    {
        if (period == "all") return (null, null);
        var date = now.ToOffset(TimeSpan.FromHours(7)).Date;
        var start = period switch {
            "week" => date.AddDays(-((int)date.DayOfWeek + 6) % 7),
            "month" => new DateTime(date.Year, date.Month, 1),
            "quarter" => new DateTime(date.Year, (date.Month - 1) / 3 * 3 + 1, 1),
            _ => throw new BusinessException("Khoảng thời gian xếp hạng không hợp lệ", 400)
        };
        var end = period == "week" ? start.AddDays(7) : start.AddMonths(period == "month" ? 1 : 3);
        return (new DateTimeOffset(start, TimeSpan.FromHours(7)).ToUniversalTime(), new DateTimeOffset(end, TimeSpan.FromHours(7)).ToUniversalTime());
    }
}
