using System.Text.Json;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using Xunit;

namespace BCOBCA.Tests;

public sealed class ProductSpecificationRulesTests
{
    private static Dictionary<string, JsonElement> Values(string json) => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    [Fact]
    public void DifferentCategoriesUseDifferentTypesAndGroups()
    {
        var phone = new[] { new CategoryAttributeDefinitionDto("battery", "Dung lượng pin", "Pin", "number", "mAh", true), new CategoryAttributeDefinitionDto("stereo", "Loa stereo", "Âm thanh", "boolean") };
        ProductSpecificationRules.ValidateDefinitions(phone);
        var values = Values("{\"battery\":3400,\"stereo\":true}");
        ProductSpecificationRules.ValidateValues(values, phone, "product");
        var groups = ProductSpecificationRules.BuildGroups(values, phone);
        Assert.Equal(new[] { "Pin", "Âm thanh" }, groups.Select(g => g.Name));
        Assert.Equal("mAh", groups[0].Items[0].Unit);
        Assert.True(groups[1].Items[0].Value.GetBoolean());
        var clothes = new[] { new CategoryAttributeDefinitionDto("material", "Chất liệu", "Chất liệu", Required: true), new CategoryAttributeDefinitionDto("size", "Kích thước", "Phân loại", Scope: "variant") };
        ProductSpecificationRules.ValidateValues(Values("{\"material\":\"Cotton\"}"), clothes, "product");
        ProductSpecificationRules.ValidateValues(Values("{\"size\":\"M\"}"), clothes, "variant");
        Assert.Throws<BusinessException>(() => ProductSpecificationRules.ValidateValues(values, clothes, "product"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"battery\":\"3400\"}")]
    [InlineData("{\"battery\":{\"value\":3400}}")]
    [InlineData("{\"battery\":3400,\"unconfigured\":1}")]
    public void RejectsMissingRequiredInvalidTypesAndUnknownFields(string json)
    {
        var definitions = new[] { new CategoryAttributeDefinitionDto("battery", "Pin", "Pin", "number", Required: true) };
        Assert.Throws<BusinessException>(() => ProductSpecificationRules.ValidateValues(Values(json), definitions, "product"));
    }

    [Fact]
    public void ValidatesSelectOptionsAndSeparatesVariantFields()
    {
        var definitions = new[] { new CategoryAttributeDefinitionDto("fit", "Kiểu dáng", "May mặc", "select", Options: new[] { "Regular", "Slim" }), new CategoryAttributeDefinitionDto("color", "Màu sắc", "Phân loại", Scope: "variant", Required: true) };
        ProductSpecificationRules.ValidateValues(Values("{\"fit\":\"Regular\"}"), definitions, "product");
        ProductSpecificationRules.ValidateValues(Values("{\"color\":\"Đen\",\"isSample\":true}"), definitions, "variant");
        Assert.Throws<BusinessException>(() => ProductSpecificationRules.ValidateValues(Values("{\"fit\":\"Unknown\"}"), definitions, "product"));
        Assert.Throws<BusinessException>(() => ProductSpecificationRules.ValidateValues(Values("{\"fit\":\"Regular\"}"), definitions, "variant"));
    }

    [Fact]
    public void InheritsParentFieldsAndAllowsChildOverrides()
    {
        var parent = new[] { new CategoryAttributeDefinitionDto("weight", "Khối lượng", "Chung", "number", "g"), new CategoryAttributeDefinitionDto("material", "Chất liệu", "Chung") };
        var child = new[] { new CategoryAttributeDefinitionDto("weight", "Trọng lượng", "Kích thước", "number", "kg") };
        var merged = ProductSpecificationRules.MergeDefinitions(new[] { parent, child });
        Assert.Equal(2, merged.Count);
        Assert.Equal("kg", merged.Single(d => d.Key == "weight").Unit);
        Assert.Contains(merged, d => d.Key == "material");
    }

    [Fact]
    public void RejectsDuplicateUnsafeOrUnknownDefinitions()
    {
        var valid = new CategoryAttributeDefinitionDto("chip", "Chip", "Hiệu năng");
        foreach (var definitions in new[] { new[] { valid, valid }, new[] { valid with { Key = "bad.key" } }, new[] { valid with { Type = "html" } }, new[] { valid with { Scope = "unknown" } }, new[] { valid with { Type = "select" } } })
            Assert.Throws<BusinessException>(() => ProductSpecificationRules.ValidateDefinitions(definitions));
    }
}
