using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed partial class AdminStoreService
{
    private async Task<AdminStoreResourceStore> ResourceStore(Guid accountId, Guid id, CancellationToken ct)
    {
        await Admin(accountId, ct);
        return await Shops.AsNoTracking().Where(s => s.Id == id).Select(s => new AdminStoreResourceStore(s.Id, s.Name)).SingleOrDefaultAsync(ct)
            ?? throw new BusinessException("Không tìm thấy cửa hàng.", 404);
    }

    public async Task<AdminStoreProductDetail> ProductAsync(Guid accountId, Guid storeId, Guid id, CancellationToken ct)
    {
        var shop = await ResourceStore(accountId, storeId, ct);
        var p = await store.Set<Product>().AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.StoreId == storeId, ct)
            ?? throw new BusinessException("Không tìm thấy sản phẩm của cửa hàng.", 404);
        var source = store.Set<ProductVariant>().AsNoTracking().Where(v => v.ProductId == id);
        var totals = await source.GroupBy(v => v.ProductId).Select(g => new { Stock = g.Sum(v => v.StockQuantity), Reserved = g.Sum(v => v.ReservedQuantity), Available = g.Sum(v => v.IsActive ? Math.Max(0, v.StockQuantity - v.ReservedQuantity) : 0), Count = g.Count() }).SingleOrDefaultAsync(ct);
        var images = await store.Set<ProductImage>().AsNoTracking().Where(i => i.ProductId == id && i.ImageUrl != "")
            .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.ImageUrl).Take(20).ToArrayAsync(ct);
        var variants = await source.OrderBy(v => v.SKU).ThenBy(v => v.Id).Take(200)
            .Select(v => new SellerProductVariantDto(v.Id, v.VariantName, v.SKU, v.Price, v.SalePrice, v.StockQuantity, v.ReservedQuantity, Math.Max(0, v.StockQuantity - v.ReservedQuantity), v.IsActive)).ToArrayAsync(ct);
        var category = await store.Set<Category>().Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "";
        var brand = await store.Set<Brand>().Where(b => b.Id == p.BrandId).Select(b => b.Name).FirstOrDefaultAsync(ct) ?? "";
        var product = new AdminStoreProduct(p.Id, p.Name, p.SKU, images.FirstOrDefault() ?? "", category, brand,
            p.Description.Length > 500 ? p.Description[..500] : p.Description, p.BasePrice, p.SalePrice,
            totals?.Stock ?? 0, totals?.Reserved ?? 0, totals?.Available ?? 0, totals?.Count ?? 0, p.Status, p.IsDeleted, p.CreatedAt, p.UpdatedAt);
        var definitions = await ProductSpecificationRules.ResolveDefinitionsAsync(store, p.CategoryId, ct);
        return new(shop, product, p.Description, images, variants,
            ProductSpecificationRules.BuildGroups(ProductSpecificationRules.ParseValues(p.Specifications), definitions),
            p.ShippingWeightGrams, p.ShippingLengthCm, p.ShippingWidthCm, p.ShippingHeightCm);
    }

    public async Task<AdminStoreOrderDetail> OrderAsync(Guid accountId, Guid storeId, Guid id, CancellationToken ct)
    {
        var shop = await ResourceStore(accountId, storeId, ct);
        var sellers = await store.Set<SellerOrder>().AsNoTracking().Where(s => s.StoreId == storeId && s.OrderId == id).ToArrayAsync(ct);
        if (sellers.Length == 0) throw new BusinessException("Không tìm thấy đơn hàng của cửa hàng.", 404);
        var order = await store.Set<Order>().AsNoTracking().SingleAsync(o => o.Id == id, ct);
        var payment = await store.Set<Payment>().AsNoTracking().Where(p => p.OrderId == id).OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
            .Select(p => new { p.PaymentMethod, p.PaidAt }).FirstOrDefaultAsync(ct);
        var source = store.Set<OrderItem>().AsNoTracking().Where(i => i.OrderId == id && i.StoreId == storeId);
        var itemCount = await source.CountAsync(ct);
        var items = await source.OrderBy(i => i.Id).Take(200).Select(i => new AdminStoreOrderLine(i.Id, i.ProductId, i.ProductName, i.SKU,
            store.Set<ProductVariant>().Where(v => v.Id == i.VariantId && v.ProductId == i.ProductId).Select(v => v.VariantName).FirstOrDefault() ?? "",
            store.Set<ProductImage>().Where(image => image.ProductId == i.ProductId && image.ImageUrl != "").OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder).ThenBy(image => image.Id).Select(image => image.ImageUrl).FirstOrDefault() ?? "",
            i.Quantity, i.UnitPrice, i.DiscountAmount, i.TotalPrice)).ToArrayAsync(ct);
        var shipments = await store.Set<Shipment>().AsNoTracking().Where(s => s.OrderId == id).OrderBy(s => s.Id).Take(50)
            .Select(s => new ShipmentDto(s.Id, s.Carrier, s.TrackingCode, s.Status, s.ShippedAt, s.DeliveredAt)).ToArrayAsync(ct);
        var history = await store.Set<OrderStatusHistory>().AsNoTracking().Where(h => h.OrderId == id).OrderByDescending(h => h.CreatedAt).ThenBy(h => h.Id).Take(200)
            .Select(h => new OrderHistoryDto(h.OldStatus, h.NewStatus, h.Note, h.CreatedAt)).ToArrayAsync(ct);
        var statuses = sellers.Select(s => s.Status).Where(s => s != "").Distinct().ToArray();
        return new(shop, order.Id, order.OrderCode, statuses.Length == 1 ? statuses[0] : order.Status, order.PaymentStatus, payment?.PaymentMethod ?? "",
            order.RecipientName, order.RecipientPhone, order.ShippingAddress, order.Note, order.CreatedAt, order.ConfirmedAt, order.CompletedAt, order.CancelledAt,
            payment?.PaidAt, sellers.Sum(s => s.Subtotal), sellers.Sum(s => s.DiscountAmount), sellers.Sum(s => s.TotalAmount), itemCount, items, shipments, history.Reverse().ToArray());
    }
}
