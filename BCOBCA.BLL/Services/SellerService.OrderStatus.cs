using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed partial class SellerService
{
    public async Task<SellerOrderDto> UpdateOrderStatusAsync(Guid accountId, Guid storeId, Guid id, OrderStatusRequest input, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct);
        await CheckAccess(accountId, storeId, ct);
        // Keep the same single-store order boundary used by seller reads.
        if (!await Orders(storeId).AnyAsync(o => o.Id == id, ct))
            throw new BusinessException("Không tìm thấy đơn hàng", 404);
        var order = await store.Set<Order>().SingleAsync(o => o.Id == id, ct);
        if (input.ConfirmCodCollected || input.Status != "CONFIRMED")
            throw new BusinessException("Shop chỉ được xác nhận đơn. Trạng thái vận chuyển do GHN cập nhật.", 400);
        if (!await store.Set<Shipment>().AnyAsync(s => s.OrderId == id && s.Carrier == "GHN" && s.GhnShopId > 0 && s.RequestJson != null, ct))
            throw new BusinessException("Đơn chưa có dữ liệu vận chuyển GHN đã lưu. Vui lòng liên hệ hỗ trợ.", 409);
        if (!CommerceRules.CanTransition(order.Status, "CONFIRMED"))
            throw new BusinessException("Trạng thái đơn đã thay đổi. Vui lòng tải lại đơn hàng.", 409);
        var now = DateTimeOffset.UtcNow;
        store.Set<OrderStatusHistory>().Add(new OrderStatusHistory {
            OrderId = id, OldStatus = order.Status, NewStatus = "CONFIRMED",
            ChangedByAccountId = accountId, CreatedAt = now,
        });
        order.Status = "CONFIRMED";
        order.ConfirmedAt = now;
        foreach (var sellerOrder in await store.Set<SellerOrder>().Where(s => s.OrderId == id).ToListAsync(ct))
            sellerOrder.Status = order.Status;
        await UserNotifications.AddAsync(store, order.UserId, "ORDER_STATUS", "Đơn hàng " + order.OrderCode,
            "Shop đã xác nhận đơn hàng. Đang chuẩn bị vận chuyển GHN.", "Order", id, ct);
        await store.SaveChangesAsync(ct);
        var result = await OrderAsync(accountId, storeId, id, ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
