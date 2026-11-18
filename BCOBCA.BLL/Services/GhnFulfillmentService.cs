using BCOBCA.BLL.Common;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed record GhnCallback(int ShopID, string OrderCode, string ClientOrderCode, string Type, string Status, DateTimeOffset Time);
public sealed class GhnFulfillmentService(ICommerceStore store, GhnQuoteClient client, IOrderService orders)
{
    public async Task<bool> CreateOneAsync(CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var shipment = await store.Set<Shipment>().Where(s => s.Carrier == "GHN" && s.Status == "PENDING" && s.RequestJson != null
            && (s.NextAttemptAt == null || s.NextAttemptAt <= now)
            && store.Set<Order>().Any(o => o.Id == s.OrderId && (o.Status == "CONFIRMED" || o.Status == "READY")))
            .OrderBy(s => s.NextAttemptAt).ThenBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (shipment is null) return false;
        try
        {
            shipment.TrackingCode = await client.CreateShipmentAsync(shipment.GhnShopId!.Value, shipment.RequestJson!, ct);
            shipment.Status = "ready_to_pick";
            var order = await store.Set<Order>().SingleAsync(o => o.Id == shipment.OrderId, ct);
            store.Set<OrderStatusHistory>().Add(new OrderStatusHistory { OrderId = order.Id, OldStatus = order.Status,
                NewStatus = "GHN:ready_to_pick", Note = "Đã tạo vận đơn GHN, chờ lấy hàng", CreatedAt = now });
            await UserNotifications.AddAsync(store, order.UserId, "ORDER_SHIPMENT", "Đơn hàng " + order.OrderCode,
                "Đã tạo vận đơn GHN, chờ lấy hàng.", "Order", order.Id, ct);
        }
        catch (BusinessException)
        {
            shipment.CreationAttempts++;
            shipment.NextAttemptAt = now.AddSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Min(shipment.CreationAttempts, 6))));
        }
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return true;
    }

    public async Task CallbackAsync(GhnCallback input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.OrderCode) || input.OrderCode.Length > 100 || input.ShopID <= 0
            || string.IsNullOrWhiteSpace(input.Status) || input.Time == default || input.Time > DateTimeOffset.UtcNow.AddMinutes(5)) throw new BusinessException("Callback GHN không hợp lệ", 400);
        input = input with { Time = input.Time.ToUniversalTime() };
        var type = input.Type?.ToLowerInvariant();
        if (type is not ("create" or "switch_status" or "cod")) return;
        var label = GhnLifecycleRules.Label(input.Status);
        await using var tx = await store.BeginTransactionAsync(ct);
        var shipment = await store.Set<Shipment>().SingleOrDefaultAsync(s => s.Carrier == "GHN" && s.TrackingCode == input.OrderCode && s.GhnShopId == input.ShopID, ct);
        if (shipment is null) throw new BusinessException("Vận đơn chưa sẵn sàng nhận trạng thái. Vui lòng gửi lại.", 409);
        var order = await store.Set<Order>().SingleAsync(o => o.Id == shipment.OrderId, ct);
        if (!string.IsNullOrEmpty(input.ClientOrderCode) && input.ClientOrderCode != order.OrderCode) throw new BusinessException("Mã đơn GHN không khớp", 400);
        if (await store.Set<ShipmentEvent>().AnyAsync(e => e.ShipmentId == shipment.Id && e.Type == type && e.OccurredAt == input.Time, ct)) return;
        if (shipment.ProviderUpdatedAt > input.Time || GhnLifecycleRules.IsTerminal(shipment.Status) && shipment.Status != input.Status) return;
        var collected = false;
        if (input.Status == "delivered")
        {
            var verified = await client.ShipmentStateAsync(input.ShopID, input.OrderCode, ct);
            if (verified.Status != "delivered") throw new BusinessException("GHN chưa xác nhận giao hàng thành công", 409);
            collected = verified.CodCollected && verified.CodAmount == order.TotalAmount;
        }
        var changed = shipment.Status != input.Status;
        store.Set<ShipmentEvent>().Add(new ShipmentEvent { ShipmentId = shipment.Id, Type = type!, Status = input.Status, OccurredAt = input.Time });
        shipment.Status = input.Status; shipment.ProviderUpdatedAt = input.Time;
        if (GhnLifecycleRules.HasPickedUp(input.Status)) shipment.ShippedAt ??= input.Time;
        if (input.Status == "delivered") shipment.DeliveredAt ??= input.Time;
        await orders.ApplyCarrierStatusAsync(order.Id, input.Status, collected, ct);
        // Commerce completion also updates shipments using legacy uppercase statuses.
        // Preserve the provider status/timestamp for subsequent callback ordering.
        shipment.Status = input.Status;
        if (input.Status == "delivered") shipment.DeliveredAt = input.Time;
        if (changed)
        {
            store.Set<OrderStatusHistory>().Add(new OrderStatusHistory { OrderId = order.Id, OldStatus = order.Status,
                NewStatus = "GHN:" + input.Status, Note = label, CreatedAt = input.Time });
            await UserNotifications.AddAsync(store, order.UserId, "ORDER_SHIPMENT", "Đơn hàng " + order.OrderCode, label, "Order", order.Id, ct);
        }
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
