using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed partial class SellerService
{
    public async Task<SellerProductOptionsDto> ProductOptionsAsync(Guid accountId, Guid storeId, CancellationToken ct)
    {
        await CheckProfileAccess(accountId, storeId, ct);
        var brands = await store.Set<Brand>().AsNoTracking().Where(b => b.Status == 1 && !b.IsDeleted && !b.Slug.StartsWith("seller-other-"))
            .OrderBy(b => b.Name).ThenBy(b => b.Id).Select(b => new SellerProductChoiceDto(b.Id, b.Name, null)).ToArrayAsync(ct);
        var categories = CategoryTree.Active(await store.Set<Category>().AsNoTracking().ToListAsync(ct))
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).Select(c => new SellerProductChoiceDto(c.Id, c.Name, c.ParentId, c.Slug)).ToArray();
        return new(brands, categories);
    }

    public async Task<SellerProductDto> CreateProductAsync(Guid accountId, Guid storeId, SellerCreateProductInput input, CancellationToken ct)
    {
        await CheckProfileAccess(accountId, storeId, ct);
        input = input with { Name = input.Name?.Trim() ?? "", Description = input.Description?.Trim() ?? "",
            ImageUrls = input.ImageUrls?.Select(url => url?.Trim() ?? "").ToArray()!,
            Variants = input.Variants?.Select(v => v is null ? null! : v with { Name = v.Name?.Trim() ?? "" }).ToArray() };
        await new SellerCreateProductValidator().ValidateAndThrowAsync(input, ct);
        var sku = "SP-" + input.RequestId.ToString("N").ToUpperInvariant();
        var previous = await ProjectProducts(Products(storeId).Where(p => p.SKU == sku)).SingleOrDefaultAsync(ct);
        if (previous is not null) return previous;
        if (!input.UseOtherBrand && !await store.Set<Brand>().AnyAsync(b => b.Id == input.BrandId && b.Status == 1 && !b.IsDeleted && !b.Slug.StartsWith("seller-other-"), ct))
            throw new BusinessException("Thương hiệu không tồn tại hoặc đã ngừng hoạt động.");
        var categories = CategoryTree.Active(await store.Set<Category>().AsNoTracking().ToListAsync(ct));
        if (!categories.Any(c => c.Id == input.CategoryId)) throw new BusinessException("Danh mục không tồn tại hoặc đã ngừng hoạt động.");
        if (catalog is null) throw new BusinessException("Chức năng tạo sản phẩm chưa được cấu hình.", 503);
        var baseSlug = Regex.Replace(input.Name.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD), @"\p{Mn}", "");
        baseSlug = Regex.Replace(baseSlug, "[^a-z0-9]+", "-").Trim('-');
        if (baseSlug.Length > 180) baseSlug = baseSlug[..180].TrimEnd('-');
        if (baseSlug.Length == 0) baseSlug = "san-pham";
        var variants = input.Variants?.Select((v, index) => new VariantInput(null, $"{sku}-V{index + 1:D3}", v.Name, v.Price, v.Stock, v.SalePrice)).ToArray()
            ?? [new VariantInput(null, sku + "-DEFAULT", "Mặc định", input.Price, input.Stock, input.SalePrice,
                JsonSerializer.Serialize(input.VariantAttributes ?? new Dictionary<string, JsonElement>(), ProductSpecificationRules.JsonOptions))];
        var representative = variants.OrderByDescending(v => v.StockQuantity > 0).ThenBy(v => v.SalePrice ?? v.Price).First();
        var specifications = input.SpecificationText is null ? input.Specifications ?? new Dictionary<string, JsonElement>()
            : new Dictionary<string, JsonElement> { ["free_text"] = JsonSerializer.SerializeToElement(input.SpecificationText.Trim()) };
        // Reuse catalog validation, inventory ledger and audit; ownership is fixed by the route, never the body.
        var id = await catalog.SaveProductAsync(accountId, null, new ProductInput(input.BrandId, input.CategoryId, input.Name,
            $"{baseSlug}-{input.RequestId:N}", sku, representative.Price, variants,
            input.ImageUrls, input.Description, representative.SalePrice, input.IsActive, false,
            specifications, storeId, input.ShippingWeightGrams,
            input.ShippingLengthCm, input.ShippingWidthCm, input.ShippingHeightCm, UseOtherBrand: input.UseOtherBrand), ct);
        return await ProjectProducts(Products(storeId).Where(p => p.Id == id)).SingleAsync(ct);
    }
}
