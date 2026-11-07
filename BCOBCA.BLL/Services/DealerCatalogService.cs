using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed class DealerCatalogService(ICommerceStore store) : IDealerCatalogService
{
    private async Task PublicStore(Guid id, CancellationToken ct)
    {
        if (!await store.Set<Store>().AsNoTracking().AnyAsync(x => x.Id == id && x.StoreType == 1 && x.Status == 1 && x.DeletedAt == null, ct))
            throw new BusinessException("Không tìm thấy đại lý", 404);
    }

    private IQueryable<Product> Products(Guid storeId, Guid[] categoryIds) => store.Set<Product>().AsNoTracking()
        .Where(p => p.Status == 1 && categoryIds.Contains(p.CategoryId) &&
            store.Set<StoreProduct>().Any(link => link.StoreId == storeId && link.ProductId == p.Id) &&
            store.Set<Brand>().Any(b => b.Id == p.BrandId && b.Status == 1));

    public async Task<PageResult<DealerProductDto>> ProductsAsync(Guid storeId, DealerProductQuery query, CancellationToken ct)
    {
        if (query.Page is < 1 or > 100000 || query.PageSize is < 1 or > 50 || query.MinPrice is < 0 or > 1000000000000 || query.MaxPrice is < 0 or > 1000000000000 ||
            query.MinPrice > query.MaxPrice || query.Sort is not ("popular" or "newest" or "price-asc" or "price-desc"))
            throw new BusinessException("Bộ lọc sản phẩm không hợp lệ", 400);
        await PublicStore(storeId, ct);
        var categories = CategoryTree.Active(await store.Set<Category>().AsNoTracking().ToListAsync(ct));
        var ids = query.CategoryId is { } id ? CategoryTree.Descendants(id, categories) : categories.Select(x => x.Id).ToArray();
        var source = from p in Products(storeId, ids)
                     from variant in store.Set<ProductVariant>().Where(v => v.ProductId == p.Id && v.IsActive)
                         .OrderByDescending(v => v.StockQuantity > v.ReservedQuantity).ThenBy(v => v.SalePrice ?? v.Price).ThenBy(v => v.Id).Take(1).DefaultIfEmpty()
                     select new {
                         p.Id, p.BrandId, p.CategoryId, p.Name,
                         ImageUrl = store.Set<ProductImage>().Where(i => i.ProductId == p.Id).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault() ?? "",
                         Price = variant == null ? p.BasePrice : variant.Price, SalePrice = variant == null ? p.SalePrice : variant.SalePrice,
                         VariantId = variant == null ? (Guid?)null : variant.Id, AvailableStock = variant == null ? 0 : variant.StockQuantity - variant.ReservedQuantity,
                         Rating = store.Set<ProductReview>().Where(r => r.ProductId == p.Id && r.Status == 1).Select(r => (double?)r.Rating).Average(),
                         ReviewCount = store.Set<ProductReview>().Count(r => r.ProductId == p.Id && r.Status == 1),
                         SoldCount = store.Set<OrderItem>().Where(item => item.ProductId == p.Id && store.Set<Order>().Any(o => o.Id == item.OrderId && o.CompletedAt != null)).Select(item => (long?)item.Quantity).Sum() ?? 0,
                         p.CreatedAt };
        if (query.MinPrice is { } min) source = source.Where(x => (x.SalePrice ?? x.Price) >= min);
        if (query.MaxPrice is { } max) source = source.Where(x => (x.SalePrice ?? x.Price) <= max);
        var total = await source.CountAsync(ct);
        var sorted = query.Sort switch
        {
            "price-asc" => source.OrderBy(x => x.SalePrice ?? x.Price).ThenBy(x => x.Id),
            "price-desc" => source.OrderByDescending(x => x.SalePrice ?? x.Price).ThenBy(x => x.Id),
            "newest" => source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => source.OrderByDescending(x => x.SoldCount).ThenByDescending(x => x.ReviewCount).ThenByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
        };
        var items = await sorted.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new DealerProductDto(x.Id, x.BrandId, x.CategoryId, x.Name, x.ImageUrl, x.Price, x.SalePrice, x.VariantId, x.AvailableStock, x.Rating, x.ReviewCount, x.SoldCount, x.CreatedAt)).ToListAsync(ct);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<DealerProductCategoryDto>> CategoriesAsync(Guid storeId, CancellationToken ct)
    {
        await PublicStore(storeId, ct);
        var categories = CategoryTree.Active(await store.Set<Category>().AsNoTracking().ToListAsync(ct));
        var counts = await Products(storeId, categories.Select(x => x.Id).ToArray()).GroupBy(x => x.CategoryId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var assigned = await store.Set<StoreCategory>().Where(x => x.StoreId == storeId).Select(x => x.CategoryId).ToArrayAsync(ct);
        var scoped = assigned.Concat(counts.Keys).SelectMany(id => CategoryTree.Descendants(id, categories)).ToHashSet();
        return categories.Where(x => scoped.Contains(x.Id) && !categories.Any(child => child.ParentId == x.Id && scoped.Contains(child.Id)))
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new DealerProductCategoryDto(x.Id, x.ParentId, x.Name, x.Slug, counts.GetValueOrDefault(x.Id))).ToArray();
    }
}
