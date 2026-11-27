using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BCOBCA.BLL.Services;
public sealed partial class AdminUserService(ICommerceStore store) : IAdminUserService
{
    private IQueryable<Account> Accounts => store.Set<Account>().Where(a => a.Role != "Admin" && a.Role != "SuperAdmin");
    private async Task Admin(Guid actor, CancellationToken ct)
    {
        if (!await store.Set<Account>().AnyAsync(a => a.Id == actor && a.Status == 1 && (a.Role == "Admin" || a.Role == "SuperAdmin"), ct))
            throw new BusinessException("Không có quyền quản lý người dùng.", 403);
    }
    private IQueryable<AdminUserRow> Rows(IQueryable<Account> source) => source.Select(a => new AdminUserRow(a.Id,
        store.Set<User>().Where(u => u.AccountId == a.Id && u.FullName != "").Select(u => u.FullName).FirstOrDefault() ?? (a.Email != "" ? a.Email : a.Phone != "" ? a.Phone : "Người dùng"),
        store.Set<User>().Where(u => u.AccountId == a.Id).Select(u => u.Nickname).FirstOrDefault() ?? "",
        store.Set<User>().Where(u => u.AccountId == a.Id).Select(u => u.AvatarUrl).FirstOrDefault() ?? "",
        a.Email, a.Phone, a.Role, a.Status == 1, a.EmailVerified, a.PhoneVerified, a.CreatedAt, a.UpdatedAt, a.LastLoginAt));
    private static int Page(int requested, int size, int count) => Math.Min(requested, Math.Max(1, (int)Math.Ceiling(count / (double)size)));

    public async Task<AdminUserSummary> SummaryAsync(Guid actor, CancellationToken ct)
    {
        await Admin(actor, ct);
        var total = await Accounts.CountAsync(ct); var active = await Accounts.CountAsync(a => a.Status == 1, ct);
        return new(total, active, total - active, await Accounts.CountAsync(a => a.EmailVerified || a.PhoneVerified, ct));
    }
    public async Task<PageResult<AdminUserRow>> ListAsync(Guid actor, AdminUserQuery query, CancellationToken ct)
    {
        await Admin(actor, ct);
        if (query.Page is < 1 or > 100000 || query.PageSize is < 1 or > 50 || query.Search?.Length > 200
            || query.Status is not ("all" or "active" or "locked") || query.Verification is not ("all" or "verified" or "unverified")
            || query.Joined is not ("all" or "week" or "month") || query.Sort is not ("newest" or "oldest" or "name")) throw new BusinessException("Bộ lọc người dùng không hợp lệ.");
        var source = Accounts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search)) { var term = query.Search.Trim().ToLower(); source = source.Where(a => a.Email.ToLower().Contains(term) || a.Phone.Contains(term)
            || store.Set<User>().Any(u => u.AccountId == a.Id && (u.FullName.ToLower().Contains(term) || u.Nickname.ToLower().Contains(term)))); }
        if (query.Status != "all") source = source.Where(a => (a.Status == 1) == (query.Status == "active"));
        if (query.Verification != "all") source = source.Where(a => (a.EmailVerified || a.PhoneVerified) == (query.Verification == "verified"));
        if (query.Joined != "all") { var date = CommerceRules.VietnamDate(DateTimeOffset.UtcNow).AddDays(query.Joined == "week" ? -6 : -29); var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime(); source = source.Where(a => a.CreatedAt >= start); }
        var count = await source.CountAsync(ct); var page = Page(query.Page, query.PageSize, count);
        source = query.Sort switch {
            "name" => source.OrderBy(a => store.Set<User>().Where(u => u.AccountId == a.Id && u.FullName != "").Select(u => u.FullName).FirstOrDefault() ?? (a.Email != "" ? a.Email : a.Phone)).ThenBy(a => a.Id),
            "oldest" => source.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id),
            _ => source.OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id)
        };
        return new(await Rows(source.Skip((page - 1) * query.PageSize).Take(query.PageSize)).ToArrayAsync(ct), page, query.PageSize, count);
    }

    public async Task<AdminUserDetail> DetailAsync(Guid actor, Guid id, AdminUserDetailQuery query, CancellationToken ct)
    {
        await Admin(actor, ct);
        if (query.PageSize is < 1 or > 20 || new[] { query.OrdersPage, query.StoresPage, query.ReviewsPage, query.HistoryPage }.Any(p => p is < 1 or > 100000)) throw new BusinessException("Phân trang chi tiết không hợp lệ.");
        var user = await Rows(Accounts.AsNoTracking().Where(a => a.Id == id)).SingleOrDefaultAsync(ct) ?? throw new BusinessException("Không tìm thấy người dùng.", 404);
        var profile = await store.Set<User>().AsNoTracking().SingleOrDefaultAsync(u => u.AccountId == id, ct);
        var orders = store.Set<Order>().AsNoTracking(); orders = profile == null ? orders.Where(o => false) : orders.Where(o => o.UserId == profile.Id);
        var shops = store.Set<Store>().AsNoTracking().Where(s => s.OwnerAccountId == id && s.StoreType == 1 && s.DeletedAt == null);
        var reviews = store.Set<ProductReview>().AsNoTracking(); reviews = profile == null ? reviews.Where(r => false) : reviews.Where(r => r.UserId == profile.Id);
        var history = store.Set<AuditLog>().AsNoTracking().Where(h => h.EntityType == "Account" && h.EntityId == id);
        var stats = new AdminUserStats(await orders.CountAsync(ct), await orders.SumAsync(o => o.Status == "COMPLETED" && o.PaymentStatus != "REFUNDED" ? o.TotalAmount : 0m, ct), await shops.CountAsync(ct), await reviews.CountAsync(ct));
        var ordersPage = Page(query.OrdersPage, query.PageSize, stats.Orders); var storesPage = Page(query.StoresPage, query.PageSize, stats.Stores);
        var reviewsPage = Page(query.ReviewsPage, query.PageSize, stats.Reviews); var historyCount = await history.CountAsync(ct); var historyPage = Page(query.HistoryPage, query.PageSize, historyCount);
        var orderRows = await orders.OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id).Skip((ordersPage - 1) * query.PageSize).Take(query.PageSize).Select(o => new AdminUserOrder(o.Id, o.OrderCode, o.Status, o.PaymentStatus, o.TotalAmount,
            store.Set<OrderItem>().Where(i => i.OrderId == o.Id).Sum(i => i.Quantity), store.Set<OrderItem>().Where(i => i.OrderId == o.Id).OrderBy(i => i.Id).Select(i => i.ProductName).FirstOrDefault() ?? "",
            store.Set<OrderItem>().Where(i => i.OrderId == o.Id).OrderBy(i => i.Id).Select(i => store.Set<ProductImage>().Where(image => image.ProductId == i.ProductId).OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder).ThenBy(image => image.Id).Select(image => image.ImageUrl).FirstOrDefault()).FirstOrDefault() ?? "", o.CreatedAt) { RecipientName = o.RecipientName, RecipientPhone = o.RecipientPhone, ItemCount = store.Set<OrderItem>().Count(i => i.OrderId == o.Id), PaymentMethod = store.Set<Payment>().Where(p => p.OrderId == o.Id).OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Select(p => p.PaymentMethod).FirstOrDefault() ?? "" }).ToArrayAsync(ct);
        var shopRows = await shops.OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Id).Skip((storesPage - 1) * query.PageSize).Take(query.PageSize)
            .Select(s => new AdminUserStore(s.Id, s.Name, s.Slug, s.LogoUrl, s.Status == 1, s.IsVerified, s.CreatedAt)).ToArrayAsync(ct);
        var reviewRows = await reviews.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Skip((reviewsPage - 1) * query.PageSize).Take(query.PageSize)
            .Select(r => new AdminUserReview(r.Id, store.Set<OrderItem>().Where(i => i.Id == r.OrderItemId).Select(i => i.ProductName).FirstOrDefault() ?? "",
                store.Set<Store>().Where(s => s.Id == r.StoreId).Select(s => s.Name).FirstOrDefault() ?? "", r.Rating, r.Content, r.Status == 1, r.ReplyContent, r.CreatedAt)).ToArrayAsync(ct);
        var historyRows = await history.OrderByDescending(h => h.CreatedAt).ThenBy(h => h.Id).Skip((historyPage - 1) * query.PageSize).Take(query.PageSize)
            .Select(h => new AdminUserHistory(h.Id, h.Action, h.Details ?? "", h.AccountId == null ? "Hệ thống" : store.Set<User>().Where(u => u.AccountId == h.AccountId && u.FullName != "").Select(u => u.FullName).FirstOrDefault() ?? "Quản trị viên", h.CreatedAt)).ToArrayAsync(ct);
        var birth = profile != null && profile.DateOfBirth >= new DateOnly(1900, 1, 1) && profile.DateOfBirth <= CommerceRules.VietnamDate(DateTimeOffset.UtcNow) ? (DateOnly?)profile.DateOfBirth : null;
        return new(user, birth, profile?.ReferralCode ?? "", stats, new(orderRows, ordersPage, query.PageSize, stats.Orders), new(shopRows, storesPage, query.PageSize, stats.Stores), new(reviewRows, reviewsPage, query.PageSize, stats.Reviews), new(historyRows, historyPage, query.PageSize, historyCount));
    }

    public async Task StatusAsync(Guid actor, Guid id, AdminUserStatusInput input, CancellationToken ct)
    {
        await Admin(actor, ct);
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000) throw new BusinessException("Nhập lý do thao tác, tối đa 1.000 ký tự.");
        var target = await store.Set<Account>().AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new BusinessException("Không tìm thấy người dùng.", 404);
        if (id == actor || target.Role is "Admin" or "SuperAdmin") throw new BusinessException("Không thay đổi tài khoản quản trị tại đây.", 403);
        try
        {
            await using var tx = await store.BeginTransactionAsync(ct); await Admin(actor, ct);
            var now = DateTimeOffset.UtcNow; var reason = input.Reason.Trim();
            var changed = await Accounts.Where(a => a.Id == id && a.UpdatedAt == input.UpdatedAt.ToUniversalTime() && (a.Status == 1) != input.Active)
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Status, (short)(input.Active ? 1 : 0)).SetProperty(a => a.UpdatedAt, now), ct);
            if (changed == 0) throw new BusinessException("Tài khoản vừa thay đổi. Vui lòng tải lại trước khi thao tác.", 409);
            if (!input.Active)
            {
                var shops = store.Set<Store>().Where(s => s.OwnerAccountId == id && s.StoreType == 1 && s.DeletedAt == null);
                var shopIds = await shops.Select(s => s.Id).ToArrayAsync(ct);
                await shops.ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Status, (short)0).SetProperty(s => s.UpdatedAt, now), ct);
                await store.Set<RefreshToken>().Where(t => t.AccountId == id && t.RevokedAt == null).ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, (DateTimeOffset?)now), ct);
                foreach (var shopId in shopIds) store.Set<AuditLog>().Add(new AuditLog { AccountId = actor, EntityType = "Store", EntityId = shopId, Action = "SUSPEND_STORE_OWNER", Details = reason, CreatedAt = now });
            }
            store.Set<AuditLog>().Add(new AuditLog { AccountId = actor, EntityType = "Account", EntityId = id, Action = input.Active ? "ACTIVATE_ACCOUNT" : "SUSPEND_ACCOUNT", Details = reason, CreatedAt = now });
            await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        catch (PostgresException e) when (e.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        { throw new BusinessException("Tài khoản vừa thay đổi. Vui lòng tải lại trước khi thao tác.", 409); }
    }
}
