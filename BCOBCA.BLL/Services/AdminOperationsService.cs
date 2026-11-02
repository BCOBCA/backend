using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed class AdminOperationsService(ICommerceStore store, IOrderService orders) : IAdminOperationsService
{
    private Task<bool> IsAdmin(Guid accountId, CancellationToken ct) => store.Set<AccountRole>().AnyAsync(x => x.AccountId == accountId && (x.Role == "Admin" || x.Role == "SuperAdmin"), ct);
    private async Task Admin(Guid accountId, CancellationToken ct) { if (!await IsAdmin(accountId, ct)) throw new BusinessException("Không có quyền quản trị", 403); }
    private static async Task<PageResult<T>> Page<T>(IQueryable<T> source, CatalogQuery query, CancellationToken ct)
    {
        var (page, size) = CatalogService.Paging(query);
        return new(await source.Skip((page - 1) * size).Take(size).ToListAsync(ct), page, size, await source.CountAsync(ct));
    }
    public async Task<PageResult<OrderSummaryDto>> OrdersAsync(Guid accountId, CatalogQuery query, CancellationToken ct)
    {
        await Admin(accountId, ct); var source = store.Set<Order>().AsNoTracking();
        if (query.Status is not null) source = source.Where(x => x.Status == query.Status);
        if (query.BrandId is not null) source = source.Where(x => store.Set<SellerOrder>().Any(s => s.OrderId == x.Id && s.BrandId == query.BrandId));
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(x => x.OrderCode.Contains(query.Search));
        return await Page(source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new OrderSummaryDto(x.Id, x.OrderCode, x.Status, x.PaymentStatus, x.TotalAmount, x.CreatedAt)), query, ct);
    }
    public async Task<OrderDto> OrderAsync(Guid accountId, Guid id, CancellationToken ct)
    {
        await Admin(accountId, ct);
        var userId = await store.Set<Order>().Where(x => x.Id == id).Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(ct) ?? throw new BusinessException("Không tìm thấy đơn hàng", 404);
        return await orders.OrderAsync(userId, id, ct);
    }
    public async Task<Guid> ShipmentAsync(Guid accountId, ShipmentInput input, CancellationToken ct)
    {
        await Admin(accountId, ct);
        if (string.IsNullOrWhiteSpace(input.Carrier) || input.Carrier.Length > 100 || string.IsNullOrWhiteSpace(input.TrackingCode) || input.TrackingCode.Length > 100) throw new BusinessException("Thông tin vận đơn không hợp lệ");
        var carrier = input.Carrier.Trim(); var tracking = input.TrackingCode.Trim();
        var existing = await store.Set<Shipment>().SingleOrDefaultAsync(x => x.OrderId == input.OrderId && x.Carrier == carrier && x.TrackingCode == tracking, ct);
        if (existing is not null) return existing.Id;
        var shipment = new Shipment { OrderId = input.OrderId, Carrier = carrier, TrackingCode = tracking, Status = "SHIPPING", ShippedAt = DateTimeOffset.UtcNow };
        if (await store.Set<Order>().AsNoTracking().AnyAsync(x => x.Id == input.OrderId && x.Status == "SHIPPING", ct))
        {
            await using var tx = await store.BeginTransactionAsync(ct);
            await Admin(accountId, ct);
            if (!await store.Set<Order>().AsNoTracking().AnyAsync(x => x.Id == input.OrderId && x.Status == "SHIPPING", ct)) throw new BusinessException("Đơn hàng đã đổi trạng thái", 409);
            store.Set<Shipment>().Add(shipment);
            store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, Action = "ADD_SHIPMENT", EntityType = "Order", EntityId = input.OrderId, CreatedAt = DateTimeOffset.UtcNow });
            await store.SaveChangesAsync(ct); await tx.CommitAsync(ct); return shipment.Id;
        }
        store.Set<Shipment>().Add(shipment);
        // Transition owns the transaction and saves this tracked shipment with order/history.
        await orders.TransitionAsync(accountId, input.OrderId, new OrderStatusRequest("SHIPPING"), ct);
        return shipment.Id;
    }
    public async Task<PageResult<ServiceRequestDto>> RequestsAsync(Guid accountId, CatalogQuery query, CancellationToken ct)
    {
        var admin = await IsAdmin(accountId, ct);
        if (!admin && !await store.Set<AccountRole>().AnyAsync(x => x.AccountId == accountId && (x.Role == "BrandOwner" || x.Role == "BrandStaff"), ct)) throw new BusinessException("Không có quyền quản lý yêu cầu tư vấn", 403);
        return await Page(from request in store.Set<ServiceRequest>().AsNoTracking() join service in store.Set<Service>() on request.ServiceId equals service.Id
            where admin || store.Set<Brand>().Any(b => b.Id == service.BrandId && b.OwnerAccountId == accountId) || store.Set<BrandMember>().Any(m => m.BrandId == service.BrandId && m.AccountId == accountId && m.Status == 1 && (m.Role == "Owner" || m.Role == "Manager"))
            orderby request.CreatedAt descending, request.Id select new ServiceRequestDto(request.Id, service.Id, service.Name, request.CustomerName, request.Phone, request.Email, request.Description, request.Status, request.CreatedAt), query, ct);
    }
    public async Task<PageResult<FeedbackDto>> FeedbacksAsync(Guid accountId, CatalogQuery query, CancellationToken ct)
    {
        await Admin(accountId, ct);
        return await Page(store.Set<Feedback>().AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new FeedbackDto(x.Id, x.Subject, x.Content, x.Status, x.AdminResponse, x.CreatedAt)), query, ct);
    }
    public async Task ReplyAsync(Guid accountId, Guid id, FeedbackReplyRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Response) || input.Response.Length > 10000) throw new BusinessException("Nội dung trả lời không hợp lệ");
        await using var tx = await store.BeginTransactionAsync(ct); await Admin(accountId, ct);
        var feedback = await store.Set<Feedback>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy phản hồi", 404);
        if (feedback.Status == "RESOLVED" && feedback.AdminResponse == input.Response.Trim()) return;
        feedback.AdminResponse = input.Response.Trim(); feedback.Status = "RESOLVED"; feedback.ResolvedAt = DateTimeOffset.UtcNow;
        await UserNotifications.AddAsync(store, feedback.UserId, "FEEDBACK_REPLY", "Phản hồi đã được trả lời", feedback.AdminResponse, "Feedback", feedback.Id, ct);
        store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, Action = "REPLY_FEEDBACK", EntityType = "Feedback", EntityId = id, CreatedAt = DateTimeOffset.UtcNow });
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
