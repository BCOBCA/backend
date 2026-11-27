namespace BCOBCA.BLL.DTOs;

public sealed record AdminStoreQuery(int Page = 1, int PageSize = 8, string? Search = null, string Status = "all", string Verification = "all", string Joined = "all", string Sort = "newest");
public sealed record AdminStoreDetailQuery(int ProductsPage = 1, int OrdersPage = 1, int ReviewsPage = 1, int HistoryPage = 1, int PageSize = 10, string? OrdersFrom = null, string? OrdersTo = null);
public sealed record AdminStoreSectionPage(int Page, int PageSize, int TotalItems);
public sealed record AdminStorePagination(AdminStoreSectionPage Products, AdminStoreSectionPage Orders, AdminStoreSectionPage Reviews, AdminStoreSectionPage History);
public sealed record AdminStoreSummary(int Total, int Pending, int Active, int Inactive, decimal? Growth);
public sealed record AdminStoreRow(Guid Id, string Name, string Slug, string LogoUrl, string OwnerName, string Email, string Phone, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string Status, bool IsVerified, bool Active, bool OwnerSuspended = false, bool CanSuspendOwner = false);
public sealed record AdminStorePreview(Guid Id, string Title, string Subtitle, decimal? Value, DateTimeOffset? CreatedAt);
public sealed record AdminStoreVerification(Guid Id, string LegalName, string TaxCode, string RepresentativeName, short Status, string? RejectionReason, DateTimeOffset CreatedAt, IReadOnlyList<string> Documents);
public sealed record AdminStoreStats(int Products, int Orders, decimal Sales, double? Rating, int Reviews);
public sealed record AdminStoreReview(Guid Id, string DisplayName, short Rating, string Content, DateTimeOffset CreatedAt, ReviewReplyDto? Reply);
public sealed record AdminStoreOrderItem(Guid ProductId, string Name, string ImageUrl, int Quantity);
public sealed record AdminStoreOrder(Guid Id, string Code, string Status, string PaymentStatus, string PaymentMethod,
    string RecipientName, string RecipientPhone, decimal TotalAmount, int ItemCount, int Quantity, DateTimeOffset CreatedAt, AdminStoreOrderItem? FirstItem);
public sealed record AdminStoreProduct(Guid Id, string Name, string SKU, string ImageUrl, string CategoryName, string BrandName, string Description,
    decimal BasePrice, decimal? SalePrice, int StockQuantity, int ReservedQuantity, int AvailableStock, int VariantCount,
    short Status, bool IsDeleted, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record AdminStoreDetail(AdminStoreRow Store, string Description, string Categories, AdminStoreStats Stats, AdminStoreVerification? Verification,
    IReadOnlyList<AdminStorePreview> Products, IReadOnlyList<AdminStorePreview> Orders, IReadOnlyList<AdminStorePreview> Reviews, IReadOnlyList<AdminStorePreview> History)
{
    public required AdminStorePagination Pagination { get; init; }
    public IReadOnlyList<AdminStoreReview> ReviewDetails { get; init; } = [];
    public IReadOnlyList<AdminStoreProduct> ProductDetails { get; init; } = [];
    public IReadOnlyList<AdminStoreOrder> OrderDetails { get; init; } = [];
}
public sealed record AdminStoreInput(string Name, string Slug, string Email, string Phone, string Description, DateTimeOffset? UpdatedAt = null);
public sealed record AdminStoreStatusInput(bool Active, string Reason, DateTimeOffset UpdatedAt);
public sealed record AdminStoreSuspensionInput(string Reason, DateTimeOffset UpdatedAt);
public sealed record AdminStoreReviewInput(Guid VerificationId, bool Approved, string Reason, DateTimeOffset UpdatedAt);
public sealed record AdminStoreDocument(string FileName, string ContentType, string Content);
