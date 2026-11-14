using System.Text.Json;

namespace BCOBCA.BLL.DTOs;

public sealed record SellerProductChoiceDto(Guid Id, string Name, Guid? ParentId = null, string? Slug = null);
public sealed record SellerProductOptionsDto(IReadOnlyList<SellerProductChoiceDto> Brands, IReadOnlyList<SellerProductChoiceDto> Categories,
    bool SupportsVariants = true, bool SupportsFreeText = true, int MaxImages = 5, bool SupportsOtherBrand = true);
public sealed record SellerCreateVariantInput(string Name, decimal Price, int Stock, decimal? SalePrice = null);
public sealed record SellerCreateProductInput(Guid RequestId, Guid BrandId, Guid CategoryId, string Name, decimal Price,
    int Stock, IReadOnlyList<string> ImageUrls, int ShippingWeightGrams, int ShippingLengthCm, int ShippingWidthCm,
    int ShippingHeightCm, string Description = "", decimal? SalePrice = null,
    bool IsActive = true, IReadOnlyDictionary<string, JsonElement>? Specifications = null,
    IReadOnlyDictionary<string, JsonElement>? VariantAttributes = null,
    IReadOnlyList<SellerCreateVariantInput>? Variants = null, string? SpecificationText = null, bool UseOtherBrand = false);
