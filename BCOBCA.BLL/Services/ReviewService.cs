using System.Text.Json;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed class ReviewService(ICommerceStore store) : IReviewService
{
    private IQueryable<ReviewDto> Project(IQueryable<ProductReview> source) => from review in source join user in store.Set<User>().IgnoreQueryFilters() on review.UserId equals user.Id
        select new ReviewDto(review.Id, review.ProductId, !user.IsDeleted && user.Nickname != "" ? user.Nickname : "Khách hàng", review.Rating, review.Content, review.Status, review.CreatedAt,
            JsonSerializer.Deserialize<string[]>(review.ImageUrls, (JsonSerializerOptions?)null)!, review.Revision,
            review.ReplyContent == null || review.RepliedAt == null ? null : new ReviewReplyDto(review.ReplyContent,
                (from item in store.Set<OrderItem>() join shop in store.Set<Store>().IgnoreQueryFilters() on item.StoreId equals shop.Id where item.Id == review.OrderItemId select shop.Name).FirstOrDefault() ?? "Cửa hàng", review.RepliedAt.Value));
    private static async Task<PageResult<T>> Page<T>(IQueryable<T> source, CatalogQuery query, CancellationToken ct)
    {
        var (page, size) = CatalogService.Paging(query);
        return new(await source.Skip((page - 1) * size).Take(size).ToListAsync(ct), page, size, await source.CountAsync(ct));
    }
    private async Task Admin(Guid accountId, CancellationToken ct)
    {
        if (!await store.Set<AccountRole>().AnyAsync(x => x.AccountId == accountId && (x.Role == "Admin" || x.Role == "SuperAdmin"), ct)) throw new BusinessException("Không có quyền duyệt đánh giá", 403);
    }
    private async Task<OrderItem> PurchasedItem(Guid userId, Guid id, CancellationToken ct)
        => await (from item in store.Set<OrderItem>() join order in store.Set<Order>() on item.OrderId equals order.Id
            where item.Id == id && order.UserId == userId && order.Status != "CANCELLED"
                && (order.CompletedAt != null || store.Set<Shipment>().Any(s => s.OrderId == order.Id && s.DeliveredAt != null))
            select item).SingleOrDefaultAsync(ct) ?? throw new BusinessException("Chỉ đánh giá sản phẩm đã mua và giao thành công", 403);

    public async Task<PageResult<ReviewDto>> ReviewsAsync(Guid productId, CatalogQuery query, CancellationToken ct)
    {
        if (!await ProductCatalogScope.Available(store).AnyAsync(x => x.Id == productId, ct)) throw new BusinessException("Không tìm thấy sản phẩm", 404);
        return await Page(Project(store.Set<ProductReview>().AsNoTracking().Where(x => x.ProductId == productId && x.Status == 1).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)), query, ct);
    }
    public async Task<ReviewDto?> MyReviewAsync(Guid userId, Guid orderItemId, CancellationToken ct)
    {
        await PurchasedItem(userId, orderItemId, ct);
        return await Project(store.Set<ProductReview>().AsNoTracking().Where(x => x.UserId == userId && x.OrderItemId == orderItemId)).SingleOrDefaultAsync(ct);
    }
    public async Task<Guid> SaveAsync(Guid userId, Guid orderItemId, ReviewInput input, CancellationToken ct)
    {
        var images = input.ImageUrls ?? [];
        if (input.Rating is < 1 or > 5 || string.IsNullOrWhiteSpace(input.Content) || input.Content.Length > 5000 || images.Count > 5 || images.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 2000 || !Uri.TryCreate(x, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))) throw new BusinessException("Nội dung đánh giá không hợp lệ");
        await using var tx = await store.BeginTransactionAsync(ct);
        var item = await PurchasedItem(userId, orderItemId, ct);
        var review = await store.Set<ProductReview>().SingleOrDefaultAsync(x => x.UserId == userId && x.OrderItemId == orderItemId, ct);
        var isNew = review is null;
        if (review is null) { review = new ProductReview { UserId = userId, OrderItemId = orderItemId, ProductId = item.ProductId, CreatedAt = DateTimeOffset.UtcNow }; store.Set<ProductReview>().Add(review); }
        if (review.Rating == input.Rating && review.Content == input.Content.Trim() && JsonSerializer.Deserialize<string[]>(review.ImageUrls)!.SequenceEqual(images)) return review.Id;
        review.Rating = input.Rating; review.Content = input.Content.Trim(); review.Status = 1; review.Revision = Guid.NewGuid();
        review.ImageUrls = JsonSerializer.Serialize(images);
        var accountId = await store.Set<User>().Where(x => x.Id == userId).Select(x => x.AccountId).SingleAsync(ct);
        store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, Action = "SUBMIT_REVIEW", EntityType = "ProductReview", EntityId = review.Id, CreatedAt = DateTimeOffset.UtcNow });
        if (isNew && item.StoreId.HasValue)
        {
            var owner = await store.Set<Store>().Where(s => s.Id == item.StoreId && s.Status == 1 && s.DeletedAt == null).Select(s => (Guid?)s.OwnerAccountId).SingleOrDefaultAsync(ct);
            if (owner.HasValue) await UserNotifications.AddAccountAsync(store, owner.Value, "REVIEW_CREATED", "Bạn có đánh giá sản phẩm mới",
                $"Khách hàng đã đánh giá {input.Rating} sao cho {item.ProductName}.", "StoreReview", item.StoreId.Value, ct);
        }
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct); return review.Id;
    }
    private async Task CheckStoreAccess(Guid accountId, Guid storeId, CancellationToken ct)
    {
        if (!await store.Set<Store>().AnyAsync(s => s.Id == storeId && s.OwnerAccountId == accountId && s.StoreType == 1 && s.Status == 1 && s.DeletedAt == null, ct))
            throw new BusinessException("Không có quyền xem đánh giá của cửa hàng", 403);
    }

    public async Task<PageResult<SellerReviewDto>> SellerReviewsAsync(Guid accountId, Guid storeId, SellerReviewQuery query, CancellationToken ct)
    {
        await CheckStoreAccess(accountId, storeId, ct);
        if (query.Rating is < 1 or > 5 || query.From > query.To || query.Search?.Length > 200)
            throw new BusinessException("Bộ lọc đánh giá không hợp lệ");
        var source = from review in store.Set<ProductReview>().AsNoTracking()
            join item in store.Set<OrderItem>() on review.OrderItemId equals item.Id
            join order in store.Set<Order>() on item.OrderId equals order.Id
            join user in store.Set<User>().IgnoreQueryFilters() on review.UserId equals user.Id
            join shop in store.Set<Store>() on item.StoreId equals shop.Id
            where item.StoreId == storeId && review.Status == 1
            select new { review, item, order, user, shop };
        if (query.Rating.HasValue) source = source.Where(x => x.review.Rating == query.Rating);
        if (query.Replied.HasValue) source = source.Where(x => (x.review.ReplyContent != null) == query.Replied);
        if (query.ProductId.HasValue) source = source.Where(x => x.review.ProductId == query.ProductId);
        var from = query.From?.ToUniversalTime();
        var to = query.To?.ToUniversalTime();
        if (from.HasValue) source = source.Where(x => x.review.CreatedAt >= from.Value);
        if (to.HasValue) source = source.Where(x => x.review.CreatedAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            source = source.Where(x => x.item.ProductName.ToLower().Contains(search) || x.order.OrderCode.ToLower().Contains(search)
                || !x.user.IsDeleted && x.user.Nickname.ToLower().Contains(search));
        }
        var (page, size) = CatalogService.Paging(new(query.Page, query.PageSize));
        var count = await source.CountAsync(ct);
        var rows = await source.OrderByDescending(x => x.review.CreatedAt).ThenBy(x => x.review.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var productIds = rows.Select(x => x.review.ProductId).Distinct().ToArray();
        var images = await store.Set<ProductImage>().AsNoTracking().Where(x => productIds.Contains(x.ProductId))
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);
        var variantIds = rows.Select(x => x.item.VariantId).Distinct().ToArray();
        var variants = await store.Set<ProductVariant>().AsNoTracking().Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.VariantName, ct);
        return new(rows.Select(x => new SellerReviewDto(x.review.Id, x.review.ProductId, x.item.ProductName,
            images.FirstOrDefault(i => i.ProductId == x.review.ProductId)?.ImageUrl, x.item.VariantId.HasValue ? variants.GetValueOrDefault(x.item.VariantId.Value, "") : "", x.order.OrderCode,
            !x.user.IsDeleted && x.user.Nickname != "" ? x.user.Nickname : "Khách hàng", x.review.Rating, x.review.Content,
            JsonSerializer.Deserialize<string[]>(x.review.ImageUrls)!, x.review.CreatedAt, x.review.Revision,
            x.review.ReplyContent == null || x.review.RepliedAt == null ? null : new ReviewReplyDto(x.review.ReplyContent, x.shop.Name, x.review.RepliedAt.Value))).ToArray(), page, size, count);
    }

    public async Task<ReviewDto> ReplyAsync(Guid accountId, Guid storeId, Guid id, ReviewReplyInput input, CancellationToken ct)
    {
        await CheckStoreAccess(accountId, storeId, ct);
        var content = input.Content?.Trim();
        if (string.IsNullOrEmpty(content) || content.Length > 2000 || input.ExpectedRevision == Guid.Empty)
            throw new BusinessException("Phản hồi cần từ 1 đến 2.000 ký tự và phiên bản đánh giá hợp lệ");
        await using var tx = await store.BeginTransactionAsync(ct);
        var review = await store.Set<ProductReview>().SingleOrDefaultAsync(r => r.Id == id && r.Status == 1
            && store.Set<OrderItem>().Any(item => item.Id == r.OrderItemId && item.StoreId == storeId), ct)
            ?? throw new BusinessException("Không tìm thấy đánh giá của cửa hàng", 404);
        if (review.Revision != input.ExpectedRevision) throw new BusinessException("Đánh giá đã thay đổi. Vui lòng tải lại trước khi phản hồi", 409);
        if (review.ReplyContent == content) return await Project(store.Set<ProductReview>().AsNoTracking().Where(r => r.Id == id)).SingleAsync(ct);
        review.ReplyContent = content;
        review.RepliedAt = DateTimeOffset.UtcNow;
        review.Revision = Guid.NewGuid();
        store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, Action = "REPLY_REVIEW", EntityType = "ProductReview", EntityId = id, CreatedAt = review.RepliedAt.Value });
        var recipient = await store.Set<User>().Where(u => u.Id == review.UserId).Select(u => (Guid?)u.AccountId).SingleOrDefaultAsync(ct);
        if (recipient.HasValue)
        {
            var shopName = await store.Set<Store>().Where(s => s.Id == storeId).Select(s => s.Name).SingleAsync(ct);
            await UserNotifications.AddAccountAsync(store, recipient.Value, "REVIEW_REPLY", "Cửa hàng đã phản hồi đánh giá của bạn",
                $"{shopName}: {(content.Length > 160 ? content[..160] + "…" : content)}", "Review", review.OrderItemId, ct);
        }
        try { await store.SaveChangesAsync(ct); }
        catch (Exception ex) when (DatabaseConflicts.IsConcurrency(ex)) { throw new BusinessException("Đánh giá đã thay đổi. Vui lòng tải lại trước khi phản hồi", 409); }
        var result = await Project(store.Set<ProductReview>().AsNoTracking().Where(r => r.Id == id)).SingleAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<PageResult<ReviewDto>> PendingAsync(Guid accountId, CatalogQuery query, CancellationToken ct)
    {
        await Admin(accountId, ct);
        return await Page(Project(store.Set<ProductReview>().AsNoTracking().Where(x => x.Status == 0).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)), query, ct);
    }
    public async Task ModerateAsync(Guid accountId, Guid id, short status, Guid expectedRevision, CancellationToken ct)
    {
        if (status is not (1 or 2)) throw new BusinessException("Trạng thái đánh giá không hợp lệ");
        await using var tx = await store.BeginTransactionAsync(ct); await Admin(accountId, ct);
        var review = await store.Set<ProductReview>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy đánh giá", 404);
        if (review.Revision != expectedRevision) throw new BusinessException("Đánh giá đã thay đổi. Vui lòng đọc lại trước khi duyệt", 409);
        if (review.Status == status) return;
        review.Status = status; store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, Action = status == 1 ? "APPROVE_REVIEW" : "REJECT_REVIEW", EntityType = "ProductReview", EntityId = id, CreatedAt = DateTimeOffset.UtcNow });
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
