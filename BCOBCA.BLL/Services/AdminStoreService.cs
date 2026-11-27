using System.Net.Mail;
using System.Text.RegularExpressions;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BCOBCA.BLL.Services;
public sealed partial class AdminStoreService(ICommerceStore store, IVerificationDocumentStorage documents) : IAdminStoreService
{
    private IQueryable<Store> Shops => store.Set<Store>().Where(s => s.StoreType == 1 && s.DeletedAt == null);
    private IQueryable<Store> Pending => Shops.Where(s => store.Set<StoreVerification>().Any(v => v.StoreId == s.Id && v.Status == 0));
    private async Task Admin(Guid id, CancellationToken ct)
    {
        if (!await store.Set<Account>().AnyAsync(a => a.Id == id && a.Status == 1 && (a.Role == "Admin" || a.Role == "SuperAdmin"), ct))
            throw new BusinessException("Không có quyền quản lý cửa hàng.", 403);
    }
    private IQueryable<AdminStoreRow> Rows(IQueryable<Store> source) => source.Select(s => new AdminStoreRow(s.Id, s.Name, s.Slug, s.LogoUrl,
        store.Set<User>().Where(u => u.AccountId == s.OwnerAccountId).Select(u => u.FullName).FirstOrDefault()
            ?? store.Set<Account>().Where(a => a.Id == s.OwnerAccountId).Select(a => a.Email).FirstOrDefault() ?? "—",
        s.Email, s.Phone, s.CreatedAt, s.UpdatedAt,
        store.Set<StoreVerification>().Any(v => v.StoreId == s.Id && v.Status == 0) ? "pending" : s.Status == 1 ? "active" : "inactive", s.IsVerified, s.Status == 1,
        !store.Set<Account>().Any(a => a.Id == s.OwnerAccountId && a.Status == 1),
        store.Set<Account>().Any(a => a.Id == s.OwnerAccountId && a.Status == 1 && a.Role != "Admin" && a.Role != "SuperAdmin")));

    public async Task<AdminStoreSummary> SummaryAsync(Guid accountId, CancellationToken ct)
    {
        await Admin(accountId, ct);
        var total = await Shops.CountAsync(ct); var pending = await Pending.CountAsync(ct);
        var active = await Shops.CountAsync(s => s.Status == 1 && !Pending.Any(p => p.Id == s.Id), ct);
        var previous = await Shops.CountAsync(s => s.CreatedAt < DateTimeOffset.UtcNow.AddDays(-7), ct);
        return new(total, pending, active, total - pending - active, AdminDashboardService.ChangePercent(total, previous));
    }
    public async Task<PageResult<AdminStoreRow>> ListAsync(Guid accountId, AdminStoreQuery query, CancellationToken ct)
    {
        await Admin(accountId, ct);
        if (query.Page < 1 || query.Page > 100000 || query.PageSize is < 1 or > 50 || query.Search?.Length > 200
            || query.Status is not ("all" or "pending" or "active" or "inactive") || query.Verification is not ("all" or "verified" or "unverified")
            || query.Joined is not ("all" or "week" or "month") || query.Sort is not ("newest" or "oldest" or "name")) throw new BusinessException("Bộ lọc cửa hàng không hợp lệ.");
        var source = Shops.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search)) { var search = query.Search.Trim().ToLower(); source = source.Where(s => s.Name.ToLower().Contains(search) || s.Phone.Contains(search) || s.Email.ToLower().Contains(search)
            || store.Set<User>().Any(u => u.AccountId == s.OwnerAccountId && u.FullName.ToLower().Contains(search)) || store.Set<Account>().Any(a => a.Id == s.OwnerAccountId && a.Email.ToLower().Contains(search))); }
        if (query.Status == "pending") source = source.Where(s => Pending.Any(p => p.Id == s.Id));
        else if (query.Status != "all") source = source.Where(s => (s.Status == 1) == (query.Status == "active") && !Pending.Any(p => p.Id == s.Id));
        if (query.Verification != "all") source = source.Where(s => s.IsVerified == (query.Verification == "verified"));
        if (query.Joined != "all") { var start = new DateTimeOffset(CommerceRules.VietnamDate(DateTimeOffset.UtcNow).AddDays(query.Joined == "week" ? -6 : -29).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime(); source = source.Where(s => s.CreatedAt >= start); }
        var total = await source.CountAsync(ct);
        source = query.Sort switch { "oldest" => source.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id), "name" => source.OrderBy(s => s.Name).ThenBy(s => s.Id), _ => source.OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Id) };
        return new(await Rows(source.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)).ToListAsync(ct), query.Page, query.PageSize, total);
    }
    public async Task<AdminStoreDetail> DetailAsync(Guid accountId, Guid id, int days, CancellationToken ct, AdminStoreDetailQuery? query = null)
    {
        await Admin(accountId, ct);
        if (days is not (7 or 30)) throw new BusinessException("Khoảng thống kê không hợp lệ.");
        query ??= new();
        if (query.PageSize is < 1 or > 20 || new[] { query.ProductsPage, query.OrdersPage, query.ReviewsPage, query.HistoryPage }.Any(p => p < 1 || p > 100000))
            throw new BusinessException("Phân trang cửa hàng không hợp lệ.");
        var shop = await Shops.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new BusinessException("Không tìm thấy cửa hàng.", 404);
        var row = await Rows(Shops.AsNoTracking().Where(s => s.Id == id)).SingleAsync(ct);
        var end = new DateTimeOffset(CommerceRules.VietnamDate(DateTimeOffset.UtcNow).AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime(); var start = end.AddDays(-days);
        var orderRows = from seller in store.Set<SellerOrder>().AsNoTracking() join order in store.Set<Order>() on seller.OrderId equals order.Id
            where seller.StoreId == id && order.CreatedAt >= start && order.CreatedAt < end select new { seller, order };
        var sellerOrders = orderRows.GroupBy(x => new { x.order.Id, x.order.OrderCode, x.order.Status, x.order.PaymentStatus, x.order.RecipientName, x.order.RecipientPhone, x.order.CreatedAt })
            .Select(g => new {
                order = g.Key,
                seller = new { TotalAmount = g.Sum(x => x.seller.TotalAmount), Status = g.Select(x => x.seller.Status).Distinct().Count() == 1 ? g.Max(x => x.seller.Status) ?? "" : "" }
            });
        var reviews = store.Set<ProductReview>().AsNoTracking().Where(r => r.StoreId == id && r.Status == 1);
        var products = store.Set<Product>().AsNoTracking().Where(p => p.StoreId == id);
        var stats = new AdminStoreStats(await products.CountAsync(ct), await sellerOrders.CountAsync(ct),
            await sellerOrders.SumAsync(x => x.order.Status == "COMPLETED" && x.order.PaymentStatus != "REFUNDED" ? x.seller.TotalAmount : 0m, ct),
            await reviews.AverageAsync(r => (double?)r.Rating, ct), await reviews.CountAsync(ct));
        var history = store.Set<AuditLog>().AsNoTracking().Where(a => a.EntityType == "Store" && a.EntityId == id);
        AdminStoreSectionPage Page(int requested, int total) => new(Math.Min(requested, Math.Max(1, (int)Math.Ceiling(total / (double)query.PageSize))), query.PageSize, total);
        var pagination = new AdminStorePagination(Page(query.ProductsPage, stats.Products), Page(query.OrdersPage, stats.Orders), Page(query.ReviewsPage, stats.Reviews), Page(query.HistoryPage, await history.CountAsync(ct)));
        var verification = await store.Set<StoreVerification>().AsNoTracking().Where(v => v.StoreId == id).OrderByDescending(v => v.Status == 0).ThenByDescending(v => v.CreatedAt).ThenBy(v => v.Id).FirstOrDefaultAsync(ct);
        var categories = await (from c in store.Set<Category>() join sc in store.Set<StoreCategory>() on c.Id equals sc.CategoryId where sc.StoreId == id select c.Name).ToListAsync(ct);
        var orderDetails = await sellerOrders.OrderByDescending(x => x.order.CreatedAt).ThenBy(x => x.order.Id)
            .Skip((pagination.Orders.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new AdminStoreOrder(x.order.Id, x.order.OrderCode, x.seller.Status != "" ? x.seller.Status : x.order.Status,
                x.order.PaymentStatus, store.Set<Payment>().Where(p => p.OrderId == x.order.Id).OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Select(p => p.PaymentMethod).FirstOrDefault() ?? "",
                x.order.RecipientName, x.order.RecipientPhone, x.seller.TotalAmount,
                store.Set<OrderItem>().Count(i => i.OrderId == x.order.Id && i.StoreId == id),
                store.Set<OrderItem>().Where(i => i.OrderId == x.order.Id && i.StoreId == id).Sum(i => i.Quantity), x.order.CreatedAt,
                store.Set<OrderItem>().Where(i => i.OrderId == x.order.Id && i.StoreId == id).OrderBy(i => i.Id)
                    .Select(i => new AdminStoreOrderItem(i.ProductId, i.ProductName,
                        store.Set<ProductImage>().Where(image => image.ProductId == i.ProductId && image.ImageUrl != "").OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder).ThenBy(image => image.Id).Select(image => image.ImageUrl).FirstOrDefault() ?? "", i.Quantity)).FirstOrDefault())).ToListAsync(ct);
        var productDetails = await products.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
            .Skip((pagination.Products.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(p => new AdminStoreProduct(p.Id, p.Name, p.SKU,
                store.Set<ProductImage>().Where(i => i.ProductId == p.Id && i.ImageUrl != "").OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault() ?? "",
                store.Set<Category>().Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault() ?? "",
                store.Set<Brand>().Where(b => b.Id == p.BrandId).Select(b => b.Name).FirstOrDefault() ?? "",
                p.Description.Length > 500 ? p.Description.Substring(0, 500) : p.Description, p.BasePrice, p.SalePrice,
                store.Set<ProductVariant>().Where(v => v.ProductId == p.Id).Sum(v => v.StockQuantity),
                store.Set<ProductVariant>().Where(v => v.ProductId == p.Id).Sum(v => v.ReservedQuantity),
                store.Set<ProductVariant>().Where(v => v.ProductId == p.Id && v.IsActive).Sum(v => Math.Max(0, v.StockQuantity - v.ReservedQuantity)),
                store.Set<ProductVariant>().Count(v => v.ProductId == p.Id), p.Status, p.IsDeleted, p.CreatedAt, p.UpdatedAt)).ToListAsync(ct);
        var reviewDetails = await reviews.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Skip((pagination.Reviews.Page - 1) * query.PageSize).Take(query.PageSize).Select(r => new AdminStoreReview(r.Id,
            store.Set<User>().Where(u => u.Id == r.UserId && u.Nickname != "").Select(u => u.Nickname).FirstOrDefault() ?? "Khách hàng",
            r.Rating, r.Content, r.CreatedAt,
            r.ReplyContent == null || r.RepliedAt == null ? null : new ReviewReplyDto(r.ReplyContent, shop.Name, r.RepliedAt.Value))).ToListAsync(ct);
        return new(row, shop.Description, string.Join(", ", categories), stats,
            verification == null ? null : new(verification.Id, verification.LegalName, verification.TaxCode, verification.RepresentativeName, verification.Status, verification.RejectionReason, verification.CreatedAt, VerificationDocuments.Summaries(verification.Documents).Select(d => d.Kind).ToList()),
            productDetails.Select(p => new AdminStorePreview(p.Id, p.Name, p.IsDeleted ? "Đã xóa" : p.Status == 1 ? "Đang bán" : "Đã ẩn", p.SalePrice ?? p.BasePrice, p.CreatedAt)).ToArray(),
            orderDetails.Select(o => new AdminStorePreview(o.Id, o.Code, o.Status, o.TotalAmount, o.CreatedAt)).ToArray(),
            reviewDetails.Select(r => new AdminStorePreview(r.Id, r.Content, "Đánh giá sản phẩm", r.Rating, r.CreatedAt)).ToArray(),
            await history.OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id).Skip((pagination.History.Page - 1) * query.PageSize).Take(query.PageSize).Select(a => new AdminStorePreview(a.Id, a.Action, a.Details ?? "", null, a.CreatedAt)).ToListAsync(ct)) { ReviewDetails = reviewDetails, ProductDetails = productDetails, OrderDetails = orderDetails, Pagination = pagination };
    }

    private async Task<Store> Lock(Guid id, DateTimeOffset expected, CancellationToken ct)
    {
        var count = await Shops.Where(s => s.Id == id && s.UpdatedAt == expected.ToUniversalTime()).ExecuteUpdateAsync(setters => setters.SetProperty(s => s.UpdatedAt, DateTimeOffset.UtcNow), ct);
        if (count == 0) throw new BusinessException("Cửa hàng vừa thay đổi. Vui lòng tải lại trước khi thao tác.", 409);
        return await Shops.SingleAsync(s => s.Id == id, ct);
    }
    private void Audit(Guid actor, Guid id, string action, string reason) => store.Set<AuditLog>().Add(new AuditLog { AccountId = actor, EntityType = "Store", EntityId = id, Action = action, Details = reason, CreatedAt = DateTimeOffset.UtcNow });
    private static string Reason(string reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 1000 ? reason.Trim() : throw new BusinessException("Nhập lý do, tối đa 1.000 ký tự.");
    public async Task<Guid> SaveAsync(Guid accountId, Guid? id, AdminStoreInput input, CancellationToken ct)
    {
        await Admin(accountId, ct);
        var name = input.Name?.Trim() ?? ""; var slug = input.Slug?.Trim().ToLowerInvariant() ?? "";
        var email = input.Email?.Trim() ?? ""; var phone = input.Phone?.Trim() ?? ""; var description = input.Description?.Trim() ?? "";
        if (name.Length is < 2 or > 200 || slug.Length is < 2 or > 200 || !Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$")
            || email.Length > 255 || email.Length > 0 && !MailAddress.TryCreate(email, out _) || phone.Length > 20 || phone.Length > 0 && !Regex.IsMatch(phone, "^[+0-9 ().-]{6,20}$") || description.Length > 5000)
            throw new BusinessException("Kiểm tra tên, đường dẫn, liên hệ và mô tả cửa hàng.");
        await using var tx = await store.BeginTransactionAsync(ct); await Admin(accountId, ct);
        if (await store.Set<Store>().IgnoreQueryFilters().AnyAsync(s => s.Slug == slug && s.Id != id, ct)) throw new BusinessException("Đường dẫn cửa hàng đã tồn tại.", 409);
        var shop = id == null ? new Store { OwnerAccountId = accountId, StoreType = 1, Status = 1 } : await Lock(id.Value, input.UpdatedAt ?? throw new BusinessException("Thiếu phiên bản cửa hàng."), ct);
        shop.Name = name; shop.Slug = slug; shop.Email = email; shop.Phone = phone; shop.Description = description;
        if (id == null) { store.Set<Store>().Add(shop); store.Set<StoreSettings>().Add(new StoreSettings { StoreId = shop.Id, ShowBranches = true }); }
        Audit(accountId, shop.Id, id == null ? "CREATE_STORE" : "UPDATE_STORE", $"{name} (@{slug})");
        try { await store.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" }) { throw new BusinessException("Đường dẫn cửa hàng đã tồn tại.", 409); }
        return shop.Id;
    }
    public async Task StatusAsync(Guid accountId, Guid id, AdminStoreStatusInput input, CancellationToken ct)
    {
        await Admin(accountId, ct); var reason = Reason(input.Reason);
        await using var tx = await store.BeginTransactionAsync(ct); await Admin(accountId, ct);
        var ownerId = await Shops.Where(s => s.Id == id).Select(s => (Guid?)s.OwnerAccountId).SingleOrDefaultAsync(ct) ?? throw new BusinessException("Không tìm thấy cửa hàng.", 404);
        // Lock owner before stores, matching account suspension, so reopen cannot race a ban.
        var owner = await store.Set<Account>().Where(a => a.Id == ownerId && a.Status == 1)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.UpdatedAt, a => a.UpdatedAt), ct);
        if (owner == 0) throw new BusinessException("Tài khoản chủ cửa hàng đang bị khóa. Không thể thay đổi trạng thái bán.", 409);
        var shop = await Lock(id, input.UpdatedAt, ct); shop.Status = (short)(input.Active ? 1 : 0);
        Audit(accountId, id, input.Active ? "ACTIVATE_STORE" : "SUSPEND_STORE", reason);
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task SuspendOwnerAsync(Guid accountId, Guid id, AdminStoreSuspensionInput input, CancellationToken ct)
    {
        await Admin(accountId, ct); var reason = Reason(input.Reason);
        await using var tx = await store.BeginTransactionAsync(ct); await Admin(accountId, ct);
        var ownerId = await Shops.Where(s => s.Id == id).Select(s => (Guid?)s.OwnerAccountId).SingleOrDefaultAsync(ct) ?? throw new BusinessException("Không tìm thấy cửa hàng.", 404);
        var owner = await store.Set<Account>().AsNoTracking().SingleOrDefaultAsync(a => a.Id == ownerId, ct) ?? throw new BusinessException("Không tìm thấy tài khoản chủ cửa hàng.", 404);
        if (owner.Id == accountId || owner.Role is "Admin" or "SuperAdmin") throw new BusinessException("Không đình chỉ tài khoản quản trị từ trang cửa hàng.", 403);
        var now = DateTimeOffset.UtcNow;
        var changed = await store.Set<Account>().Where(a => a.Id == ownerId && a.Status == 1 && a.Role != "Admin" && a.Role != "SuperAdmin")
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Status, (short)0).SetProperty(a => a.UpdatedAt, now), ct);
        if (changed == 0) throw new BusinessException("Trạng thái tài khoản đã thay đổi. Vui lòng tải lại.", 409);
        await Lock(id, input.UpdatedAt, ct);
        var affected = await Shops.Where(s => s.OwnerAccountId == ownerId).Select(s => s.Id).ToListAsync(ct);
        await Shops.Where(s => s.OwnerAccountId == ownerId).ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Status, (short)0).SetProperty(s => s.UpdatedAt, now), ct);
        await store.Set<RefreshToken>().Where(t => t.AccountId == ownerId && t.RevokedAt == null).ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, (DateTimeOffset?)now), ct);
        foreach (var shopId in affected) Audit(accountId, shopId, "SUSPEND_STORE_OWNER", reason);
        store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, EntityType = "Account", EntityId = ownerId, Action = "SUSPEND_ACCOUNT", Details = reason, CreatedAt = now });
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task ReviewAsync(Guid accountId, Guid id, AdminStoreReviewInput input, CancellationToken ct)
    {
        await Admin(accountId, ct); var reason = input.Approved ? "Duyệt hồ sơ xác minh" : Reason(input.Reason);
        await using var tx = await store.BeginTransactionAsync(ct); await Admin(accountId, ct);
        var shop = await Lock(id, input.UpdatedAt, ct);
        var verification = await store.Set<StoreVerification>().SingleOrDefaultAsync(v => v.StoreId == id && v.Id == input.VerificationId && v.Status == 0, ct)
            ?? throw new BusinessException("Hồ sơ không còn chờ duyệt.", 409);
        verification.Status = (short)(input.Approved ? 1 : 2); verification.RejectionReason = input.Approved ? null : reason;
        verification.ReviewedByAccountId = accountId; verification.ReviewedAt = verification.UpdatedAt = DateTimeOffset.UtcNow;
        if (input.Approved) { shop.IsVerified = true; shop.VerifiedAt = DateTimeOffset.UtcNow; }
        Audit(accountId, id, input.Approved ? "APPROVE_STORE_VERIFICATION" : "REJECT_STORE_VERIFICATION", reason);
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<AdminStoreDocument> DocumentAsync(Guid accountId, Guid id, Guid verificationId, string kind, CancellationToken ct)
    {
        await Admin(accountId, ct);
        if (!await Shops.AnyAsync(s => s.Id == id, ct)) throw new BusinessException("Không tìm thấy cửa hàng.", 404);
        var verification = await store.Set<StoreVerification>().AsNoTracking().SingleOrDefaultAsync(v => v.StoreId == id && v.Id == verificationId, ct) ?? throw new BusinessException("Không tìm thấy hồ sơ.", 404);
        var document = VerificationDocuments.Read(verification.Documents).SingleOrDefault(d => d.Kind == kind && VerificationDocuments.Kinds.Contains(d.Kind)) ?? throw new BusinessException("Không tìm thấy giấy tờ.", 404);
        if (document.ContentType is not ("application/pdf" or "image/jpeg" or "image/png")) throw new BusinessException("Định dạng giấy tờ không hợp lệ.");
        return new(document.FileName, document.ContentType, Convert.ToBase64String(await documents.ReadAsync(document, ct)));
    }
}
