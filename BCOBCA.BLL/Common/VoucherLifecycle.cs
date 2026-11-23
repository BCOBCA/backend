namespace BCOBCA.BLL.Common;

public static class VoucherLifecycle
{
    public static string Status(string status, DateTimeOffset starts, DateTimeOffset expires, int? limit, int used, DateTimeOffset now)
        => status is not ("ACTIVE" or "MEMBER_OFFER") ? "DISABLED" : expires <= now ? "EXPIRED"
            : limit is not null && used >= limit ? "EXHAUSTED" : starts > now ? "UPCOMING" : "ACTIVE";
}
