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
            store.Set<ProductReviewImage>().Where(x => x.ProductReviewId == review.Id).OrderBy(x => x.SortOrder).Select(x => x.ImageUrl).ToArray(), review.Revision);
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
        => await (from item in store.Set<OrderItem>() join order in store.Set<Order>() on item.OrderId equals order.Id where item.Id == id && order.UserId == userId && order.CompletedAt != null select item).SingleOrDefaultAsync(ct) ?? throw new BusinessException("Chỉ đánh giá dòng hàng đã mua và hoàn thành", 403);

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
        if (review is null) { review = new ProductReview { UserId = userId, OrderItemId = orderItemId, ProductId = item.ProductId, CreatedAt = DateTimeOffset.UtcNow }; store.Set<ProductReview>().Add(review); }
        var oldImages = await store.Set<ProductReviewImage>().Where(x => x.ProductReviewId == review.Id).OrderBy(x => x.SortOrder).ToListAsync(ct);
        if (review.Rating == input.Rating && review.Content == input.Content.Trim() && oldImages.Select(x => x.ImageUrl).SequenceEqual(images)) return review.Id;
        review.Rating = input.Rating; review.Content = input.Content.Trim(); review.Status = 0; review.Revision = Guid.NewGuid();
        store.Set<ProductReviewImage>().RemoveRange(oldImages);
        store.Set<ProductReviewImage>().AddRange(images.Select((url, index) => new ProductReviewImage { ProductReviewId = review.Id, ImageUrl = url, SortOrder = index }));
        var accountId = await store.Set<User>().Where(x => x.Id == userId).Select(x => x.AccountId).SingleAsync(ct);
        store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, Action = "SUBMIT_REVIEW", EntityType = "ProductReview", EntityId = review.Id, CreatedAt = DateTimeOffset.UtcNow });
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct); return review.Id;
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
