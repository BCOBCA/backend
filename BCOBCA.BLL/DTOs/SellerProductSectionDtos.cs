namespace BCOBCA.BLL.DTOs;

public sealed record SellerProductVariantUpdateInput(Guid Id, string? Name = null, decimal? Price = null, decimal? SalePrice = null, int? Stock = null);
public sealed record SellerProductSectionInput(DateTimeOffset ExpectedUpdatedAt, string? Name = null, Guid? CategoryId = null,
    string? Description = null, IReadOnlyList<string>? ImageUrls = null, IReadOnlyList<SellerProductVariantUpdateInput>? Variants = null,
    string? SpecificationText = null, int? ShippingWeightGrams = null, int? ShippingLengthCm = null, int? ShippingWidthCm = null,
    int? ShippingHeightCm = null, bool? IsActive = null);
