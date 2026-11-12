using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed class AffiliateCatalogService(ICommerceStore store) : IAffiliateCatalogService
{
    public async Task<PageResult<AffiliateProductDto>> ProductsAsync(Guid userId, CatalogQuery query, CancellationToken ct)
    {
        var (page, size) = CatalogService.Paging(query);
        if (!await (from affiliate in store.Set<AffiliateProfile>() join user in store.Set<User>() on affiliate.UserId equals user.Id
            join account in store.Set<Account>() on user.AccountId equals account.Id
            where user.Id == userId && affiliate.Status == "ACTIVE" && account.Status == 1 select affiliate.Id).AnyAsync(ct))
            throw new BusinessException("Affiliate chưa hoạt động", 409);
        var rank = await LoyaltyQueries.AffiliateRankAsync(store, userId, ct);
        var source = ProductCatalogScope.Available(store).Where(p => p.AffiliateEnabled);
        var categories = query.CategoryId is not null || !string.IsNullOrWhiteSpace(query.Search)
            ? CategoryTree.Active(await store.Set<Category>().AsNoTracking().ToListAsync(ct)) : [];
        if (query.CategoryId is { } categoryId)
        {
            var ids = CategoryTree.Descendants(categoryId, categories);
            source = source.Where(p => ids.Contains(p.CategoryId));
        }
        if (query.BrandId is { } brandId) source = source.Where(p => p.BrandId == brandId);
        if (query.StoreId is { } storeId) source = source.Where(p => p.StoreId == storeId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            var categoryIds = categories.Where(c => c.Name.ToLowerInvariant().Contains(search))
                .SelectMany(c => CategoryTree.Descendants(c.Id, categories)).Distinct().ToArray();
            source = source.Where(p => p.Name.ToLower().Contains(search) || categoryIds.Contains(p.CategoryId));
        }
        var priced = source.Select(p => new { Product = p, EffectivePrice =
            store.Set<ProductVariant>().Where(v => v.ProductId == p.Id && v.IsActive && v.StockQuantity > v.ReservedQuantity).Select(v => (decimal?)(v.SalePrice ?? v.Price)).Min()
            ?? store.Set<ProductVariant>().Where(v => v.ProductId == p.Id && v.IsActive).Select(v => (decimal?)(v.SalePrice ?? v.Price)).Min() ?? p.BasePrice });
        var ordered = query.Sort switch
        {
            "name" => priced.OrderBy(p => p.Product.Name).ThenBy(p => p.Product.Id),
            "price-asc" => priced.OrderBy(p => p.EffectivePrice).ThenBy(p => p.Product.Id),
            "price-desc" => priced.OrderByDescending(p => p.EffectivePrice).ThenBy(p => p.Product.Id),
            _ => priced.OrderByDescending(p => p.Product.CreatedAt).ThenBy(p => p.Product.Id)
        };
        var projected = new CatalogService(store).ProjectProducts(ordered.Select(p => p.Product), userId);
        var total = await source.CountAsync(ct);
        var items = await projected.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var productIds = items.Select(p => p.Id).ToArray();
        var policies = await store.Set<Product>().AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var ratings = await store.Set<ProductReview>().AsNoTracking().Where(r => productIds.Contains(r.ProductId) && r.Status == 1)
            .GroupBy(r => r.ProductId).Select(g => new { ProductId = g.Key, Rating = g.Average(r => (double)r.Rating), Count = g.Count() }).ToDictionaryAsync(r => r.ProductId, ct);
        var spent = await (from commission in store.Set<AffiliateCommission>() join item in store.Set<OrderItem>() on commission.OrderItemId equals item.Id
            where productIds.Contains(item.ProductId) && commission.Status != "REVERSED" group commission by item.ProductId into totals
            select new { ProductId = totals.Key, Amount = totals.Sum(c => c.CommissionAmount) }).ToDictionaryAsync(p => p.ProductId, p => p.Amount, ct);
        var result = items.Select(item =>
        {
            var policy = policies[item.Id];
            var rate = Math.Min(rank.CommissionRate, policy.AffiliateMaxRate ?? 1m);
            var estimate = decimal.Round((item.SalePrice ?? item.Price ?? 0m) * rate, 2, MidpointRounding.AwayFromZero);
            if (policy.AffiliateBudget is decimal budget) estimate = Math.Min(estimate, Math.Max(0, budget - spent.GetValueOrDefault(item.Id)));
            ratings.TryGetValue(item.Id, out var rating);
            return new AffiliateProductDto(item.Id, item.Name, item.ImageUrl, item.Price, item.SalePrice, item.VariantId, item.AvailableStock, rating?.Rating, rating?.Count ?? 0, rate, estimate);
        }).ToArray();
        return new(result, page, size, total);
    }
}
