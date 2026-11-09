using System.Text.Json;
using System.Text.RegularExpressions;
using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Common;

public static class ProductSpecificationRules
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static bool ValidKey(string? key) => key is not null && Regex.IsMatch(key, "^[a-z][a-z0-9_]{0,63}$");
    private static bool HasValue(JsonElement value) => value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) && (value.ValueKind != JsonValueKind.String || !string.IsNullOrWhiteSpace(value.GetString()));

    public static void ValidateDefinitions(IReadOnlyList<CategoryAttributeDefinitionDto> definitions)
    {
        if (definitions.Count > 100 || definitions.Any(d => d is null) || definitions.Select(d => (d.Scope, d.Key)).Distinct().Count() != definitions.Count)
            throw new BusinessException("Bộ thuộc tính bị trùng hoặc vượt giới hạn 100 trường.");
        foreach (var d in definitions)
        {
            if (!ValidKey(d.Key) || string.IsNullOrWhiteSpace(d.Label) || d.Label.Length > 120 || string.IsNullOrWhiteSpace(d.Group) || d.Group.Length > 100 || d.Unit?.Length > 32 || d.Type is not ("text" or "number" or "boolean" or "select") || d.Scope is not ("product" or "variant"))
                throw new BusinessException("Định nghĩa thuộc tính không hợp lệ.");
            if (d.Type == "select" && (d.Options is null || d.Options.Count is < 1 or > 100 || d.Options.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 200) || d.Options.Distinct(StringComparer.Ordinal).Count() != d.Options.Count))
                throw new BusinessException($"Danh sách lựa chọn của {d.Label} không hợp lệ.");
            if (d.Type != "select" && d.Options?.Count > 0) throw new BusinessException("Chỉ thuộc tính select được khai báo danh sách lựa chọn.");
        }
        if (JsonSerializer.Serialize(definitions, JsonOptions).Length > 64000) throw new BusinessException("Bộ thuộc tính quá dài.");
    }

    public static IReadOnlyList<CategoryAttributeDefinitionDto> ParseDefinitions(string json)
    {
        try
        {
            var result = JsonSerializer.Deserialize<List<CategoryAttributeDefinitionDto>>(json, JsonOptions) ?? throw new JsonException();
            ValidateDefinitions(result);
            return result;
        }
        catch (JsonException) { throw new BusinessException("Cấu hình thuộc tính danh mục không hợp lệ."); }
    }

    public static Dictionary<string, JsonElement> ParseValues(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw new BusinessException("Thông số sản phẩm phải là một đối tượng JSON."); }
    }

    public static void ValidateValues(IReadOnlyDictionary<string, JsonElement> values, IReadOnlyList<CategoryAttributeDefinitionDto> definitions, string scope)
    {
        if (values.Count > 100 || JsonSerializer.Serialize(values, JsonOptions).Length > 64000) throw new BusinessException("Thông số vượt giới hạn cho phép.");
        var fields = definitions.Where(d => d.Scope == scope).ToDictionary(d => d.Key, StringComparer.Ordinal);
        // Legacy variant objects retain their existing contract when no variant schema exists.
        if (scope == "variant" && fields.Count == 0) return;
        foreach (var (key, value) in values)
        {
            if (scope == "variant" && key == "isSample" && value.ValueKind is JsonValueKind.True or JsonValueKind.False) continue;
            if (!ValidKey(key) || fields.Count > 0 && !fields.ContainsKey(key)) throw new BusinessException($"Thuộc tính {key} không thuộc bộ thuộc tính {scope}.");
            if (!HasValue(value)) continue;
            fields.TryGetValue(key, out var field);
            var valid = field?.Type switch
            {
                "number" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
                "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "select" => value.ValueKind == JsonValueKind.String && field.Options!.Contains(value.GetString(), StringComparer.Ordinal),
                "text" => value.ValueKind == JsonValueKind.String,
                _ => value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
            };
            if (!valid || value.ValueKind == JsonValueKind.String && value.GetString()!.Length > 4000) throw new BusinessException($"Giá trị của {field?.Label ?? key} không đúng kiểu hoặc quá dài.");
        }
        foreach (var field in fields.Values.Where(d => d.Required))
            if (!values.TryGetValue(field.Key, out var value) || !HasValue(value)) throw new BusinessException($"Vui lòng nhập {field.Label}.");
    }

    public static IReadOnlyList<CategoryAttributeDefinitionDto> MergeDefinitions(IEnumerable<IReadOnlyList<CategoryAttributeDefinitionDto>> ancestors)
    {
        var merged = new Dictionary<(string Scope, string Key), CategoryAttributeDefinitionDto>();
        foreach (var definitions in ancestors)
        {
            ValidateDefinitions(definitions);
            foreach (var d in definitions) merged[(d.Scope, d.Key)] = d;
        }
        if (merged.Count > 200) throw new BusinessException("Bộ thuộc tính kế thừa vượt giới hạn 200 trường.");
        return merged.Values.ToArray();
    }

    public static async Task<IReadOnlyList<CategoryAttributeDefinitionDto>> ResolveDefinitionsAsync(ICommerceStore store, Guid categoryId, CancellationToken ct)
    {
        var chain = new List<IReadOnlyList<CategoryAttributeDefinitionDto>>();
        var visited = new HashSet<Guid>();
        Guid? current = categoryId;
        while (current is not null)
        {
            if (chain.Count > 32 || !visited.Add(current.Value)) throw new BusinessException("Cấu trúc danh mục bị vòng lặp hoặc quá sâu.");
            var category = await store.Set<Category>().AsNoTracking().Where(c => c.Id == current).Select(c => new { c.ParentId, c.AttributeDefinitions }).SingleOrDefaultAsync(ct) ?? throw new BusinessException("Danh mục không tồn tại.", 404);
            chain.Add(ParseDefinitions(category.AttributeDefinitions));
            current = category.ParentId;
        }
        chain.Reverse();
        return MergeDefinitions(chain);
    }

    public static IReadOnlyList<ProductSpecificationGroupDto> BuildGroups(IReadOnlyDictionary<string, JsonElement> values, IReadOnlyList<CategoryAttributeDefinitionDto> definitions)
    {
        var fields = definitions.Where(d => d.Scope == "product").ToArray();
        var rows = new List<(string Group, ProductSpecificationDto Item)>();
        foreach (var field in fields)
            if (values.TryGetValue(field.Key, out var value) && HasValue(value) && value.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object))
                rows.Add((field.Group, new(field.Key, field.Label, value, field.Unit)));
        var known = fields.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var (key, value) in values.Where(v => !known.Contains(v.Key)).OrderBy(v => v.Key, StringComparer.Ordinal))
            if (HasValue(value) && value.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object)) rows.Add(("Thông tin khác", new(key, key, value)));
        return rows.GroupBy(r => r.Group).Select(g => new ProductSpecificationGroupDto(g.Key, g.Select(r => r.Item).ToArray())).ToArray();
    }
}
