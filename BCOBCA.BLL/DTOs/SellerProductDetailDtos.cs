namespace BCOBCA.BLL.DTOs;

public sealed record SellerProductVariantDto(Guid Id, string Name, string SKU, decimal Price, decimal? SalePrice,
    int Stock, int Reserved, int Available, bool Active);
public sealed record SellerProductDetailDto(SellerProductDto Product, string SKU, string Description,
    IReadOnlyList<string> ImageUrls, IReadOnlyList<SellerProductVariantDto> Variants,
    IReadOnlyList<ProductSpecificationGroupDto> Specifications, int? ShippingWeightGrams, int? ShippingLengthCm,
    int? ShippingWidthCm, int? ShippingHeightCm, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    Guid CategoryId = default, bool SupportsSectionUpdates = true);
