using System.Text.Json;

namespace BCOBCA.BLL.DTOs;

public sealed record SellerProductChoiceDto(Guid Id, string Name, Guid? ParentId = null);
public sealed record SellerProductOptionsDto(IReadOnlyList<SellerProductChoiceDto> Brands, IReadOnlyList<SellerProductChoiceDto> Categories);
public sealed record SellerCreateProductInput(Guid RequestId, Guid BrandId, Guid CategoryId, string Name, decimal Price,
    int Stock, IReadOnlyList<string> ImageUrls, int ShippingWeightGrams, int ShippingLengthCm, int ShippingWidthCm,
    int ShippingHeightCm, string Description = "", string ShortDescription = "", decimal? SalePrice = null,
    bool IsActive = true, IReadOnlyDictionary<string, JsonElement>? Specifications = null,
    IReadOnlyDictionary<string, JsonElement>? VariantAttributes = null);
