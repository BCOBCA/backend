namespace BCOBCA.BLL.DTOs;

public sealed record CatalogQuery(int Page = 1, int PageSize = 20, string? Search = null, Guid? BrandId = null, Guid? CategoryId = null, string Sort = "newest", string? Status = null, Guid? StoreId = null);
public sealed record BrandDto(Guid Id, string Name, string Slug, string LogoUrl, string CoverUrl, string Description, string Website, string Phone);
public sealed record CategoryDto(Guid Id, Guid? ParentId, string Name, string Slug, string ImageUrl);
public sealed record VariantDto(Guid Id, string Name, string SKU, decimal Price, decimal? SalePrice, int AvailableStock, IReadOnlyDictionary<string, string>? Attributes = null, bool IsSample = false);
public sealed record ProductSellerDto(Guid Id, string Name, string Slug, string LogoUrl, string Description, string? Phone);
public sealed record CatalogItemDto(Guid Id, Guid BrandId, string Name, string Slug, string Description, string ImageUrl, decimal? Price, decimal? SalePrice, bool IsService, Guid? VariantId, int AvailableStock, bool IsFavorite = false, IReadOnlyList<VariantDto>? Variants = null, IReadOnlyList<string>? ImageUrls = null, IReadOnlyList<ProductSpecificationGroupDto>? Specifications = null, string? SKU = null, IReadOnlyList<ProductSellerDto>? Sellers = null, Guid? StoreId = null);
public sealed record BannerDto(Guid Id, string ImageUrl, string TargetUrl);
public sealed record ArticleDto(Guid Id, string Title, string Slug, string ThumbnailUrl, string Summary, string Content, DateTimeOffset? PublishedAt);
public sealed record ConsultationRequest(Guid ServiceId, string CustomerName, string Phone, string Email, string Description);
