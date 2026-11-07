namespace BCOBCA.BLL.DTOs;

public sealed record DealerProductQuery(int Page = 1, int PageSize = 15, Guid? CategoryId = null, decimal? MinPrice = null, decimal? MaxPrice = null, string Sort = "popular");
public sealed record DealerProductDto(Guid Id, Guid BrandId, Guid CategoryId, string Name, string ImageUrl, decimal Price, decimal? SalePrice, Guid? VariantId, int AvailableStock, double? Rating, int ReviewCount, long SoldCount, DateTimeOffset CreatedAt);
public sealed record DealerProductCategoryDto(Guid Id, Guid? ParentId, string Name, string Slug, int ProductCount);
