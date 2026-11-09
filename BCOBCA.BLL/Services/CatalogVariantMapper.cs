using System.Text.Json;
using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Entities;

namespace BCOBCA.BLL.Services;

public static class CatalogVariantMapper
{
    public static VariantDto ToDto(ProductVariant variant)
    {
        var attributes = new Dictionary<string, string>();
        var sample = false;
        try
        {
            using var json = JsonDocument.Parse(variant.Attributes);
            if (json.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in json.RootElement.EnumerateObject())
                {
                    if (property.Name.Equals("isSample", StringComparison.OrdinalIgnoreCase))
                    {
                        sample = property.Value.ValueKind == JsonValueKind.True;
                        continue;
                    }
                    var value = property.Value.ValueKind switch { JsonValueKind.String => property.Value.GetString(), JsonValueKind.Number => property.Value.GetRawText(), _ => null };
                    if (!string.IsNullOrWhiteSpace(value)) attributes[property.Name] = value.Trim();
                }
            }
        }
        catch (JsonException) { /* Legacy malformed attributes must not hide purchasable variants. */ }
        return new VariantDto(variant.Id, variant.VariantName, variant.SKU, variant.Price, variant.SalePrice, variant.StockQuantity - variant.ReservedQuantity, attributes, sample);
    }
}
