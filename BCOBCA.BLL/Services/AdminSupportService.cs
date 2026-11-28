using System.Globalization;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed partial class AdminOperationsService
{
    public static (DateTimeOffset? Start, DateTimeOffset? End) ValidateSupportQuery(AdminSupportQuery q)
    {
        if (q.Page is < 1 or > 100000 || q.PageSize is not (10 or 20 or 50) || q.Search.Length > 200 || q.Subject.Length > 200
            || q.Status is not ("all" or "OPEN" or "RESOLVED")) throw new BusinessException("Bộ lọc hỗ trợ không hợp lệ.");
        if (q.From is null && q.To is null) return (null, null);
        if (!DateOnly.TryParseExact(q.From, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
            || !DateOnly.TryParseExact(q.To, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to)
            || from < new DateOnly(2000, 1, 1) || to > CommerceRules.VietnamDate(DateTimeOffset.UtcNow)
            || to.DayNumber - from.DayNumber is < 0 or > 92) throw new BusinessException("Chọn khoảng ngày hợp lệ, tối đa 93 ngày.");
        return (new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime(),
            new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime());
    }

    public async Task<AdminSupportPage> SupportAsync(Guid accountId, AdminSupportQuery q, CancellationToken ct)
    {
        await Admin(accountId, ct);
        var (start, end) = ValidateSupportQuery(q);
        var all = store.Set<Feedback>().AsNoTracking();
        var total = await all.CountAsync(ct);
        var resolved = await all.CountAsync(f => f.Status == "RESOLVED", ct);
        var source = all;
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var search = q.Search.Trim().ToLowerInvariant();
            var code = search.StartsWith("#ht-") ? search[4..] : search.StartsWith("ht-") ? search[3..] : search.TrimStart('#');
            source = source.Where(f => f.Subject.ToLower().Contains(search) || f.Content.ToLower().Contains(search)
                || f.AdminResponse.ToLower().Contains(search) || code != "" && f.Id.ToString().ToLower().Contains(code));
        }
        if (!string.IsNullOrEmpty(q.Subject)) source = source.Where(f => f.Subject == q.Subject);
        if (q.Status == "OPEN") source = source.Where(f => f.Status != "RESOLVED");
        if (q.Status == "RESOLVED") source = source.Where(f => f.Status == "RESOLVED");
        if (start.HasValue) source = source.Where(f => f.CreatedAt >= start.Value && f.CreatedAt < end!.Value);
        var count = await source.CountAsync(ct);
        var page = Math.Min(q.Page, Math.Max(1, (int)Math.Ceiling(count / (double)q.PageSize)));
        var items = await source.OrderByDescending(f => f.CreatedAt).ThenBy(f => f.Id).Skip((page - 1) * q.PageSize).Take(q.PageSize)
            .Select(f => new FeedbackDto(f.Id, f.Subject, f.Content, f.Status == "RESOLVED" ? "RESOLVED" : "OPEN", f.AdminResponse, f.CreatedAt)).ToArrayAsync(ct);
        return new(items, page, q.PageSize, count, new(total, total - resolved, resolved),
            await all.Select(f => f.Subject).Distinct().OrderBy(subject => subject).ToArrayAsync(ct));
    }
}
