namespace BCOBCA.BLL.DTOs;

public sealed record ResourceIdDto(Guid Id);
public sealed record BrandInput(string Name, string Slug, string Description = "", string LogoUrl = "", string CoverUrl = "", string Website = "", string ContactEmail = "", string Phone = "", string Address = "", bool IsActive = true, Guid? OwnerAccountId = null);
public sealed record CategoryInput(string Name, string Slug, Guid? ParentId = null, string IconUrl = "", string ImageUrl = "", int SortOrder = 0, bool IsActive = true);
public sealed record VariantInput(Guid? Id, string SKU, string VariantName, decimal Price, int StockQuantity, decimal? SalePrice = null, string Attributes = "{}", bool IsActive = true);
public sealed record ProductInput(Guid BrandId, Guid CategoryId, string Name, string Slug, string SKU, decimal BasePrice, IReadOnlyList<VariantInput> Variants, IReadOnlyList<string> ImageUrls, string Description = "", string ShortDescription = "", decimal? SalePrice = null, bool IsActive = true, bool IsFeatured = false);
public sealed record ServiceInput(Guid BrandId, string Name, string Slug, string Description, string ThumbnailUrl = "", decimal? PriceFrom = null, string PricingType = "CONSULTATION", bool IsActive = true);
public sealed record BannerInput(string ImageUrl, string TargetUrl, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsActive = true);
public sealed record ArticleInput(string Title, string Slug, string Summary, string Content, string ThumbnailUrl = "", Guid? BrandId = null, bool IsPublished = false);
public sealed record VoucherTargetInput(Guid? BrandId = null, Guid? CategoryId = null, Guid? ProductId = null);
public sealed record VoucherInput(string Code, string Name, string Description, string DiscountType, decimal DiscountValue, decimal MinOrderAmount, int PerUserLimit, DateTimeOffset StartsAt, DateTimeOffset ExpiresAt, IReadOnlyList<VoucherTargetInput> Targets, decimal? MaxDiscount = null, int? UsageLimit = null, bool IsActive = true);
public sealed record QuotationInput(Guid RequestId, decimal Amount, string Description, DateTimeOffset ValidUntil);
public sealed record AccountRoleInput(Guid AccountId, string Role, bool Granted);
