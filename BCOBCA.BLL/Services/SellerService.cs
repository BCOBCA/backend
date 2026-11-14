using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed partial class SellerService(ICommerceStore store, GhnQuoteClient? shipping = null, IAdminCatalogService? catalog = null) : ISellerService
{
    public async Task<SellerStoreDto> OnboardAsync(Guid accountId, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct);
        var account = await store.Set<Account>().SingleOrDefaultAsync(a => a.Id == accountId && a.Status == 1, ct)
            ?? throw new BusinessException("Tài khoản không hoạt động", 403);
        var user = await store.Set<User>().SingleOrDefaultAsync(u => u.AccountId == accountId, ct)
            ?? throw new BusinessException("Không tìm thấy hồ sơ", 404);
        var shop = await SellerProvisioning.EnsureAsync(store, account, user, ct);
        await store.SaveChangesAsync(ct);
        var result = (await StoresAsync(accountId, ct)).Single(s => s.Id == shop.Id);
        await tx.CommitAsync(ct);
        return result;
    }

    private IQueryable<Store> Owned(Guid accountId) => store.Set<Store>().AsNoTracking()
        .Where(s => s.OwnerAccountId == accountId && s.StoreType == 1 && s.Status == 1 && s.DeletedAt == null);

    private async Task CheckAccess(Guid accountId, Guid storeId, CancellationToken ct)
    {
        if (!await Owned(accountId).AnyAsync(s => s.Id == storeId, ct))
            throw new BusinessException("Không tìm thấy cửa hàng thuộc tài khoản của bạn", 404);
    }

    public async Task<IReadOnlyList<SellerStoreDto>> StoresAsync(Guid accountId, CancellationToken ct)
        => await Owned(accountId).OrderBy(s => s.Name).ThenBy(s => s.Id).Select(s => new SellerStoreDto(s.Id, s.Name, s.Phone,
            store.Set<StoreBranch>().Where(b => b.StoreId == s.Id && b.IsActive).OrderByDescending(b => b.IsMain).ThenBy(b => b.Id)
                .Select(b => b.Address).FirstOrDefault() ?? "")).ToListAsync(ct);

    private IQueryable<Product> Products(Guid storeId) => store.Set<Product>().AsNoTracking()
        .Where(p => p.StoreId == storeId && !p.IsDeleted);

    private IQueryable<SellerProductDto> ProjectProducts(IQueryable<Product> source) => source
        .Select(p => new SellerProductDto(p.Id, p.Name,
            store.Set<ProductVariant>().Where(v => v.ProductId == p.Id && v.IsActive).OrderByDescending(v => v.StockQuantity > v.ReservedQuantity)
                .ThenBy(v => v.SalePrice ?? v.Price).ThenBy(v => v.Id).Select(v => (decimal?)(v.SalePrice ?? v.Price)).FirstOrDefault() ?? p.SalePrice ?? p.BasePrice,
            Math.Max(0, store.Set<ProductVariant>().Where(v => v.ProductId == p.Id && v.IsActive).Sum(v => (int?)(v.StockQuantity - v.ReservedQuantity)) ?? 0),
            p.Status == 1, store.Set<Category>().Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault() ?? "",
            store.Set<ProductImage>().Where(i => i.ProductId == p.Id).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).ThenBy(i => i.Id)
                .Select(i => i.ImageUrl).FirstOrDefault() ?? "", Math.Max(1, store.Set<ProductVariant>().Count(v => v.ProductId == p.Id && v.IsActive))));

    // Historical mixed-store orders cannot expose another seller's lines or financial totals.
    private IQueryable<Order> Orders(Guid storeId) => store.Set<Order>().AsNoTracking()
        .Where(o => store.Set<OrderItem>().Any(i => i.OrderId == o.Id && i.StoreId == storeId)
            && !store.Set<OrderItem>().Any(i => i.OrderId == o.Id && (i.StoreId == null || i.StoreId != storeId)));

    private IQueryable<SellerOrderDto> ProjectOrders(IQueryable<Order> source) => source.Select(o => new SellerOrderDto(
        o.Id, o.OrderCode, o.Status, o.RecipientName, o.RecipientPhone, o.ShippingAddress, o.CreatedAt,
        o.Subtotal, o.DiscountAmount, o.ShippingFee, o.ShippingDiscountAmount, o.TotalAmount,
        store.Set<OrderItem>().Where(i => i.OrderId == o.Id).OrderBy(i => i.Id)
            .Select(i => new SellerItemDto(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.TotalPrice)).ToList()));

    private static void ValidatePage(CatalogQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || query.Page > int.MaxValue / query.PageSize)
            throw new BusinessException("Phân trang không hợp lệ", 400);
    }

    public async Task<PageResult<SellerProductDto>> ProductsAsync(Guid accountId, Guid storeId, CatalogQuery query, CancellationToken ct)
    {
        await CheckAccess(accountId, storeId, ct);
        ValidatePage(query);
        var source = Products(storeId);
        var total = await source.CountAsync(ct);
        var items = await ProjectProducts(source.OrderBy(p => p.Name).ThenBy(p => p.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)).ToListAsync(ct);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<PageResult<SellerOrderDto>> OrdersAsync(Guid accountId, Guid storeId, CatalogQuery query, CancellationToken ct)
    {
        await CheckAccess(accountId, storeId, ct);
        ValidatePage(query);
        var source = Orders(storeId);
        var total = await source.CountAsync(ct);
        var items = await ProjectOrders(source.OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)).ToListAsync(ct);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<SellerOrderDto> OrderAsync(Guid accountId, Guid storeId, Guid id, CancellationToken ct)
    {
        await CheckAccess(accountId, storeId, ct);
        return await ProjectOrders(Orders(storeId).Where(o => o.Id == id)).SingleOrDefaultAsync(ct)
            ?? throw new BusinessException("Không tìm thấy đơn hàng của cửa hàng", 404);
    }

    public async Task<SellerSummaryDto> SummaryAsync(Guid accountId, Guid storeId, CancellationToken ct)
    {
        await CheckAccess(accountId, storeId, ct);
        var summary = await Orders(storeId).GroupBy(_ => 1).Select(g => new
        {
            Revenue = g.Sum(o => o.Status == "COMPLETED" ? o.Subtotal - o.DiscountAmount : 0m),
            Total = g.Count(), Pending = g.Count(o => o.Status == "PENDING"),
            Ready = g.Count(o => o.Status == "CONFIRMED" || o.Status == "READY"), Shipping = g.Count(o => o.Status == "SHIPPING")
        }).SingleOrDefaultAsync(ct);
        var lowStock = await Products(storeId).CountAsync(p => p.Status == 1 &&
            (store.Set<ProductVariant>().Where(v => v.ProductId == p.Id && v.IsActive).Sum(v => (int?)(v.StockQuantity - v.ReservedQuantity)) ?? 0) <= 5, ct);
        return new(summary?.Revenue ?? 0, summary?.Total ?? 0, summary?.Pending ?? 0, summary?.Ready ?? 0, summary?.Shipping ?? 0, lowStock);
    }
}
