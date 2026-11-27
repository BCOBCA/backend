using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed partial class AdminUserService
{
    public async Task<AdminUserOrderDetail> OrderAsync(Guid actor, Guid id, Guid orderId, CancellationToken ct)
    {
        await Admin(actor, ct);
        var user = await Rows(Accounts.AsNoTracking().Where(a => a.Id == id)).SingleOrDefaultAsync(ct)
            ?? throw new BusinessException("Không tìm thấy người dùng.", 404);
        var order = await store.Set<Order>().AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId
            && store.Set<User>().Any(u => u.AccountId == id && u.Id == o.UserId), ct)
            ?? throw new BusinessException("Không tìm thấy đơn hàng của người dùng.", 404);
        var payment = await store.Set<Payment>().AsNoTracking().Where(p => p.OrderId == orderId).OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
            .Select(p => new { p.PaymentMethod, p.PaidAt }).FirstOrDefaultAsync(ct);
        var source = store.Set<OrderItem>().AsNoTracking().Where(i => i.OrderId == orderId);
        var itemCount = await source.CountAsync(ct);
        var items = await source.OrderBy(i => i.Id).Take(200).Select(i => new AdminStoreOrderLine(i.Id, i.ProductId, i.ProductName, i.SKU,
            store.Set<ProductVariant>().Where(v => v.Id == i.VariantId && v.ProductId == i.ProductId).Select(v => v.VariantName).FirstOrDefault() ?? "",
            store.Set<ProductImage>().Where(image => image.ProductId == i.ProductId && image.ImageUrl != "").OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder).ThenBy(image => image.Id).Select(image => image.ImageUrl).FirstOrDefault() ?? "",
            i.Quantity, i.UnitPrice, i.DiscountAmount, i.TotalPrice)).ToArrayAsync(ct);
        var shipments = await store.Set<Shipment>().AsNoTracking().Where(s => s.OrderId == orderId).OrderBy(s => s.Id).Take(50)
            .Select(s => new ShipmentDto(s.Id, s.Carrier, s.TrackingCode, s.Status, s.ShippedAt, s.DeliveredAt)).ToArrayAsync(ct);
        var history = await store.Set<OrderStatusHistory>().AsNoTracking().Where(h => h.OrderId == orderId).OrderByDescending(h => h.CreatedAt).ThenBy(h => h.Id).Take(200)
            .Select(h => new OrderHistoryDto(h.OldStatus, h.NewStatus, h.Note, h.CreatedAt)).ToArrayAsync(ct);
        return new(id, user.FullName, order.Id, order.OrderCode, order.Status, order.PaymentStatus, payment?.PaymentMethod ?? "",
            order.RecipientName, order.RecipientPhone, order.ShippingAddress, order.Note, order.CreatedAt, order.ConfirmedAt, order.CompletedAt, order.CancelledAt,
            payment?.PaidAt, order.Subtotal, order.DiscountAmount, order.ShippingFee, order.ShippingDiscountAmount, order.TotalAmount, itemCount, items, shipments, history.Reverse().ToArray());
    }
}
