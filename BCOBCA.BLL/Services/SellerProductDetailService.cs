using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed partial class SellerService
{
    public async Task<SellerProductDetailDto> ProductAsync(Guid accountId, Guid storeId, Guid id, CancellationToken ct)
    {
        await CheckProfileAccess(accountId, storeId, ct);
        var product = await Products(storeId).SingleOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new BusinessException("Không tìm thấy sản phẩm của cửa hàng.", 404);
        var summary = await ProjectProducts(Products(storeId).Where(p => p.Id == id)).SingleAsync(ct);
        var images = await store.Set<ProductImage>().AsNoTracking().Where(i => i.ProductId == id)
            .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.ImageUrl).ToArrayAsync(ct);
        var variants = await store.Set<ProductVariant>().AsNoTracking().Where(v => v.ProductId == id)
            .OrderBy(v => v.SKU).ThenBy(v => v.Id).Select(v => new SellerProductVariantDto(v.Id, v.VariantName, v.SKU,
                v.Price, v.SalePrice, v.StockQuantity, v.ReservedQuantity, Math.Max(0, v.StockQuantity - v.ReservedQuantity), v.IsActive)).ToArrayAsync(ct);
        var definitions = await ProductSpecificationRules.ResolveDefinitionsAsync(store, product.CategoryId, ct);
        return new(summary, product.SKU, product.Description, images, variants,
            ProductSpecificationRules.BuildGroups(ProductSpecificationRules.ParseValues(product.Specifications), definitions),
            product.ShippingWeightGrams, product.ShippingLengthCm, product.ShippingWidthCm, product.ShippingHeightCm,
            product.CreatedAt, product.UpdatedAt, product.CategoryId);
    }
}
