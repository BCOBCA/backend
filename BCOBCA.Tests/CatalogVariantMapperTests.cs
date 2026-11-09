using BCOBCA.BLL.Services;
using BCOBCA.DAL.Entities;
using Xunit;

namespace BCOBCA.Tests;

public sealed class CatalogVariantMapperTests
{
    [Fact]
    public void ReturnsOptionsAndSampleFlagAlongsideTheSamePriceAndStock()
    {
        var variant = new ProductVariant { VariantName = "Bạc / 128 GB", SKU = "SILVER128", Price = 1000, SalePrice = 800, StockQuantity = 7, ReservedQuantity = 2, Attributes = "{\"color\":\"Bạc\",\"storage\":\"128 GB\",\"isSample\":true}" };
        var dto = CatalogVariantMapper.ToDto(variant);
        Assert.Equal(variant.Id, dto.Id);
        Assert.Equal(800m, dto.SalePrice);
        Assert.Equal(5, dto.AvailableStock);
        Assert.Equal("Bạc", dto.Attributes!["color"]);
        Assert.Equal("128 GB", dto.Attributes["storage"]);
        Assert.True(dto.IsSample);
        Assert.False(dto.Attributes.ContainsKey("isSample"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{broken")]
    [InlineData("[]")]
    [InlineData("null")]
    public void LegacyOrMalformedAttributesKeepTheVariantUsable(string json)
    {
        var dto = CatalogVariantMapper.ToDto(new ProductVariant { Attributes = json });
        Assert.NotNull(dto.Attributes);
        Assert.Empty(dto.Attributes);
        Assert.False(dto.IsSample);
    }

    [Fact]
    public void OnlyExposesNonemptyScalarOptions()
    {
        var dto = CatalogVariantMapper.ToDto(new ProductVariant { Attributes = "{\"color\":\"Đen\",\"size\":42,\"nested\":{},\"hidden\":false,\"empty\":\" \"}" });
        Assert.Equal(2, dto.Attributes!.Count);
        Assert.Equal("42", dto.Attributes["size"]);
    }
}
