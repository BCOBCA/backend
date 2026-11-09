using System.Text.Json;

namespace BCOBCA.BLL.DTOs;

public sealed record CategoryAttributeDefinitionDto(string Key, string Label, string Group, string Type = "text", string? Unit = null, bool Required = false, string Scope = "product", IReadOnlyList<string>? Options = null);
public sealed record ProductSpecificationDto(string Key, string Label, JsonElement Value, string? Unit = null);
public sealed record ProductSpecificationGroupDto(string Name, IReadOnlyList<ProductSpecificationDto> Items);
