using System.Globalization;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed partial class AdminStoreService
{
    public static (DateTimeOffset Start, DateTimeOffset End) ValidateProductQuery(AdminProductQuery q)
    {
        if (q.Page is < 1 or > 100000 || q.PageSize is not (10 or 20 or 50) || q.Search.Length > 200
            || q.Status is not ("all" or "active" or "hidden") || q.Stock is not ("all" or "available" or "out" or "low")
            || q.Sort is not ("newest" or "oldest" or "priceAsc" or "priceDesc" or "sold")
            || q.MinPrice < 0 || q.MaxPrice < 0 || q.MinPrice > q.MaxPrice
            || q.MinPrice > 9007199254740991m || q.MaxPrice > 9007199254740991m
            || q.MinPrice.HasValue && decimal.Truncate(q.MinPrice.Value) != q.MinPrice.Value
            || q.MaxPrice.HasValue && decimal.Truncate(q.MaxPrice.Value) != q.MaxPrice.Value)
            throw new BusinessException("Bộ lọc sản phẩm không hợp lệ.");
        if (!DateOnly.TryParseExact(q.From, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
            || !DateOnly.TryParseExact(q.To, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to)
            || from < new DateOnly(2000, 1, 1) || to > CommerceRules.VietnamDate(DateTimeOffset.UtcNow)
            || to.DayNumber - from.DayNumber is < 0 or > 92)
            throw new BusinessException("Chọn khoảng ngày hợp lệ, tối đa 93 ngày.");
        return (new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime(),
            new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime());
    }

    public async Task<AdminProductPage> ProductsAsync(Guid accountId, AdminProductQuery q, CancellationToken ct)
    {
        await Admin(accountId, ct);
        var (start, end) = ValidateProductQuery(q);
        var products = store.Set<Product>().AsNoTracking().Where(p => !p.IsDeleted && Shops.Any(s => s.Id == p.StoreId));
        var sales = from item in store.Set<OrderItem>().AsNoTracking()
                    join order in store.Set<Order>() on item.OrderId equals order.Id
                    where order.Status == "COMPLETED" && order.PaymentStatus != "REFUNDED"
                        && order.CompletedAt >= start && order.CompletedAt < end
                    select item;
        var reviews = store.Set<ProductReview>().AsNoTracking().Where(r => r.Status == 1);
        var scopedSales = sales.Where(i => products.Any(p => p.Id == i.ProductId && p.StoreId == i.StoreId));
        var summary = new AdminProductSummary(await products.CountAsync(ct),
            await scopedSales.SumAsync(i => (int?)i.Quantity, ct) ?? 0,
            await scopedSales.SumAsync(i => (decimal?)i.TotalPrice, ct) ?? 0,
            await reviews.Where(r => products.Any(p => p.Id == r.ProductId && p.StoreId == r.StoreId)).AverageAsync(r => (double?)r.Rating, ct));
        var source = products.Select(p => new { p.Id, StoreId = p.StoreId!.Value,
            StoreName = Shops.Where(s => s.Id == p.StoreId).Select(s => s.Name).FirstOrDefault() ?? "",
            p.CategoryId, CategoryName = store.Set<Category>().Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault() ?? "",
            p.Name, p.SKU, ImageUrl = store.Set<ProductImage>().Where(i => i.ProductId == p.Id).OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault() ?? "",
            Price = p.SalePrice ?? p.BasePrice,
            AvailableStock = store.Set<ProductVariant>().Where(v => v.ProductId == p.Id && v.IsActive).Sum(v => (int?)(v.StockQuantity - v.ReservedQuantity)) ?? 0,
            p.Status, p.CreatedAt,
            Sold = sales.Where(i => i.ProductId == p.Id && i.StoreId == p.StoreId).Sum(i => (int?)i.Quantity) ?? 0,
            Revenue = sales.Where(i => i.ProductId == p.Id && i.StoreId == p.StoreId).Sum(i => (decimal?)i.TotalPrice) ?? 0,
            Rating = reviews.Where(r => r.ProductId == p.Id && r.StoreId == p.StoreId).Average(r => (double?)r.Rating) });
        if (!string.IsNullOrWhiteSpace(q.Search)) { var search = q.Search.Trim().ToLower(); source = source.Where(p => p.Name.ToLower().Contains(search) || p.SKU.ToLower().Contains(search)); }
        if (q.StoreId.HasValue) source = source.Where(p => p.StoreId == q.StoreId);
        if (q.CategoryId.HasValue) source = source.Where(p => p.CategoryId == q.CategoryId);
        if (q.MinPrice.HasValue) source = source.Where(p => p.Price >= q.MinPrice);
        if (q.MaxPrice.HasValue) source = source.Where(p => p.Price <= q.MaxPrice);
        if (q.Status != "all") source = source.Where(p => (p.Status == 1) == (q.Status == "active"));
        if (q.Stock == "out") source = source.Where(p => p.AvailableStock == 0);
        if (q.Stock == "available") source = source.Where(p => p.AvailableStock > 0);
        if (q.Stock == "low") source = source.Where(p => p.AvailableStock > 0 && p.AvailableStock <= 10);
        var total = await source.CountAsync(ct);
        var page = Math.Min(q.Page, Math.Max(1, (int)Math.Ceiling(total / (double)q.PageSize)));
        source = q.Sort switch {
            "oldest" => source.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),
            "priceAsc" => source.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "priceDesc" => source.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            "sold" => source.OrderByDescending(p => p.Sold).ThenBy(p => p.Id),
            _ => source.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
        };
        var items = await source.Skip((page - 1) * q.PageSize).Take(q.PageSize)
            .Select(p => new AdminProductRow(p.Id, p.StoreId, p.StoreName, p.CategoryId, p.CategoryName, p.Name, p.SKU, p.ImageUrl,
                p.Price, p.AvailableStock, p.Status, p.CreatedAt, p.Sold, p.Revenue, p.Rating)).ToArrayAsync(ct);
        return new(items, page, q.PageSize, total, summary,
            await Shops.OrderBy(s => s.Name).ThenBy(s => s.Id).Select(s => new AdminProductOption(s.Id, s.Name)).ToArrayAsync(ct),
            await store.Set<Category>().Where(c => products.Any(p => p.CategoryId == c.Id)).OrderBy(c => c.Name).ThenBy(c => c.Id).Select(c => new AdminProductOption(c.Id, c.Name)).ToArrayAsync(ct));
    }
}
