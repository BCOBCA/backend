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
        if (input.ConfirmCodCollected || input.Status is not ("CONFIRMED" or "SHIPPING"))
            throw new BusinessException("Shop chỉ được xác nhận hoặc bàn giao đơn hàng", 400);
        // The seller UI groups CONFIRMED and READY as waiting for handover.
        var steps = order.Status == "CONFIRMED" && input.Status == "SHIPPING"
            ? new[] { "READY", "SHIPPING" } : new[] { input.Status };
        foreach (var status in steps)
        {
            if (!CommerceRules.CanTransition(order.Status, status))
                throw new BusinessException("Trạng thái đơn đã thay đổi. Vui lòng tải lại đơn hàng.", 409);
            var now = DateTimeOffset.UtcNow;
            store.Set<OrderStatusHistory>().Add(new OrderStatusHistory {
                OrderId = id, OldStatus = order.Status, NewStatus = status,
                ChangedByAccountId = accountId, CreatedAt = now,
            });
            order.Status = status;
            if (status == "CONFIRMED") order.ConfirmedAt = now;
        }
        foreach (var sellerOrder in await store.Set<SellerOrder>().Where(s => s.OrderId == id).ToListAsync(ct))
            sellerOrder.Status = order.Status;
        await UserNotifications.AddAsync(store, order.UserId, "ORDER_STATUS", "Đơn hàng " + order.OrderCode,
            order.Status == "CONFIRMED" ? "Đã xác nhận" : "Đang giao", "Order", id, ct);
        await store.SaveChangesAsync(ct);
        var result = await OrderAsync(accountId, storeId, id, ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
