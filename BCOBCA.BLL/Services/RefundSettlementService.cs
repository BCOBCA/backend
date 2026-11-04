using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BCOBCA.BLL.Services;

public sealed class RefundSettlementService(ICommerceStore store, IConfiguration config) : IRefundSettlementService
{
    private async Task RequireAdmin(Guid accountId, CancellationToken ct)
    {
        if (!await store.Set<AccountRole>().AnyAsync(x => x.AccountId == accountId && (x.Role == "Admin" || x.Role == "SuperAdmin"), ct)) throw new BusinessException("Không có quyền quản trị tài chính", 403);
    }
    private void Audit(Guid actor, string action, string entity, Guid id) => store.Set<AuditLog>().Add(new AuditLog { AccountId = actor, Action = action, EntityType = entity, EntityId = id, CreatedAt = DateTimeOffset.UtcNow });
    private static async Task<PageResult<T>> Page<T>(IQueryable<T> source, CatalogQuery query, CancellationToken ct)
    {
        var (page, size) = CatalogService.Paging(query);
        return new(await source.Skip((page - 1) * size).Take(size).ToListAsync(ct), page, size, await source.CountAsync(ct));
    }

    public async Task<Guid> RequestRefundAsync(Guid userId, Guid orderId, string idempotencyKey, RefundRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 2000 || string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100) throw new BusinessException("Lý do hoặc mã yêu cầu hoàn tiền không hợp lệ");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
        await using var tx = await store.BeginTransactionAsync(ct);
        var order = await store.Set<Order>().SingleOrDefaultAsync(x => x.Id == orderId && x.UserId == userId, ct) ?? throw new BusinessException("Không tìm thấy đơn hàng", 404);
        var payment = await store.Set<Payment>().SingleAsync(x => x.OrderId == orderId, ct);
        var prior = await store.Set<Refund>().SingleOrDefaultAsync(x => x.PaymentId == payment.Id && x.IdempotencyKey == idempotencyKey, ct);
        if (prior is not null) { if (prior.RequestHash != hash) throw new BusinessException("Mã yêu cầu đã dùng cho nội dung khác", 409); return prior.Id; }
        if (order.Status != "COMPLETED" || order.PaymentStatus != "PAID" || payment.Amount <= 0) throw new BusinessException("Đơn hàng chưa đủ điều kiện yêu cầu hoàn tiền", 409);
        if (await store.Set<Refund>().AnyAsync(x => x.PaymentId == payment.Id && x.Status != "REJECTED", ct)) throw new BusinessException("Đơn hàng đã có yêu cầu hoàn tiền", 409);
        var refund = new Refund { PaymentId = payment.Id, Amount = payment.Amount, Reason = input.Reason.Trim(), Status = "PENDING", IdempotencyKey = idempotencyKey, RequestHash = hash, CreatedAt = DateTimeOffset.UtcNow };
        store.Set<Refund>().Add(refund); await store.SaveChangesAsync(ct); await tx.CommitAsync(ct); return refund.Id;
    }

    private IQueryable<RefundDto> RefundQuery(Guid? userId) => from refund in store.Set<Refund>().AsNoTracking() join payment in store.Set<Payment>() on refund.PaymentId equals payment.Id join order in store.Set<Order>() on payment.OrderId equals order.Id where userId == null || order.UserId == userId
        orderby refund.CreatedAt descending, refund.Id select new RefundDto(refund.Id, order.Id, refund.Amount, refund.Reason, refund.Status, refund.ProviderRefundCode, refund.RestockReturnedItems, refund.CreatedAt, refund.CompletedAt);
    public Task<PageResult<RefundDto>> RefundsAsync(Guid userId, CatalogQuery query, CancellationToken ct) => Page(RefundQuery(userId), query, ct);
    public async Task<PageResult<RefundDto>> AdminRefundsAsync(Guid accountId, CatalogQuery query, CancellationToken ct) { await RequireAdmin(accountId, ct); return await Page(RefundQuery(null), query, ct); }

    public async Task SetRefundStatusAsync(Guid accountId, Guid id, RefundStatusRequest input, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct); await RequireAdmin(accountId, ct);
        var refund = await store.Set<Refund>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy yêu cầu hoàn tiền", 404);
        if (refund.Status == input.Status)
        {
            if (input.Status == "COMPLETED" && (refund.ProviderRefundCode != input.ProviderRefundCode || refund.RestockReturnedItems != input.RestockReturnedItems)) throw new BusinessException("Xác nhận hoàn tiền không khớp", 409);
            return;
        }
        if (input.Status == "APPROVED" && refund.Status == "PENDING") refund.Status = "APPROVED";
        else if (input.Status == "REJECTED" && refund.Status == "PENDING") refund.Status = "REJECTED";
        else if (input.Status == "COMPLETED" && refund.Status == "APPROVED")
        {
            if (string.IsNullOrWhiteSpace(input.ProviderRefundCode) || input.ProviderRefundCode.Length > 150) throw new BusinessException("Cần mã xác nhận hoàn tiền thực tế");
            var payment = await store.Set<Payment>().SingleAsync(x => x.Id == refund.PaymentId, ct);
            var order = await store.Set<Order>().SingleAsync(x => x.Id == payment.OrderId, ct);
            if (payment.Status != "PAID" || order.Status != "COMPLETED" || refund.Amount != payment.Amount) throw new BusinessException("Giao dịch hoàn tiền không khớp", 409);
            await ReverseRewardsAsync(order, refund.Id, ct);
            await ReverseSettlementsAsync(order.Id, refund.Id, ct);
            if (input.RestockReturnedItems)
            {
                foreach (var reservation in await store.Set<InventoryReservation>().Where(x => x.OrderId == order.Id && x.Status == "CONSUMED").ToListAsync(ct))
                {
                    var variant = await store.Set<ProductVariant>().SingleAsync(x => x.Id == reservation.VariantId, ct);
                    variant.StockQuantity = checked(variant.StockQuantity + reservation.Quantity); reservation.Status = "RETURNED";
                    store.Set<InventoryMovement>().Add(new InventoryMovement { OrderId = order.Id, VariantId = variant.Id, Quantity = reservation.Quantity, Type = "RETURN", CreatedAt = DateTimeOffset.UtcNow });
                }
            }
            refund.Status = "COMPLETED"; refund.ProviderRefundCode = input.ProviderRefundCode; refund.RestockReturnedItems = input.RestockReturnedItems; refund.CompletedAt = DateTimeOffset.UtcNow;
            payment.Status = "REFUNDED"; order.PaymentStatus = "REFUNDED"; order.Status = "REFUNDED";
            foreach (var seller in await store.Set<SellerOrder>().Where(x => x.OrderId == order.Id).ToListAsync(ct)) seller.Status = "REFUNDED";
            store.Set<PaymentTransaction>().Add(new PaymentTransaction { PaymentId = payment.Id, Provider = "COD", TransactionCode = input.ProviderRefundCode, EventId = "refund:" + refund.Id, Amount = -refund.Amount, Status = "REFUNDED", ResponseData = "{}", CreatedAt = DateTimeOffset.UtcNow });
            store.Set<OrderStatusHistory>().Add(new OrderStatusHistory { OrderId = order.Id, OldStatus = "COMPLETED", NewStatus = "REFUNDED", ChangedByAccountId = accountId, Note = "Hoàn tiền " + input.ProviderRefundCode, CreatedAt = DateTimeOffset.UtcNow });
            await UserNotifications.AddAsync(store, order.UserId, "REFUND_COMPLETED", "Đã hoàn tiền đơn " + order.OrderCode, "Mã xác nhận hoàn tiền: " + input.ProviderRefundCode, "Order", order.Id, ct);
        }
        else throw new BusinessException("Chuyển trạng thái hoàn tiền không hợp lệ", 409);
        Audit(accountId, "REFUND_" + input.Status, "Refund", id); await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private async Task ReverseRewardsAsync(Order order, Guid refundId, CancellationToken ct)
    {
        var member = await store.Set<UserMembership>().SingleAsync(x => x.UserId == order.UserId, ct);
        var earned = await store.Set<PointTransaction>().SingleOrDefaultAsync(x => x.MembershipId == member.Id && x.ReferenceId == order.Id && x.ReferenceType == "Order" && x.Type == "ORDER_EARN", ct);
        if (earned is not null)
        {
            if (member.TotalQualifiedSpend < order.Subtotal - order.DiscountAmount) throw new BusinessException("Doanh số thành viên không khớp", 409);
            member.TotalQualifiedSpend -= order.Subtotal - order.DiscountAmount; RewardLedger.Reverse(store, member, earned.Points, refundId);
            await LoyaltyQueries.RefreshMembershipAsync(store, member, ct);
        }
        var commissions = await (from commission in store.Set<AffiliateCommission>() join item in store.Set<OrderItem>() on commission.OrderItemId equals item.Id where item.OrderId == order.Id && (commission.Status == "PENDING" || commission.Status == "APPROVED" || commission.Status == "PAID") select commission).ToListAsync(ct);
        foreach (var group in commissions.GroupBy(x => x.AffiliateProfileId))
        {
            var wallet = await store.Set<AffiliateWallet>().SingleAsync(x => x.AffiliateProfileId == group.Key, ct);
            var pending = group.Where(x => x.Status == "PENDING").Sum(x => x.CommissionAmount); var approved = group.Where(x => x.Status is "APPROVED" or "PAID").Sum(x => x.CommissionAmount);
            if (wallet.PendingBalance < pending) throw new BusinessException("Sổ cái hoa hồng chờ duyệt không khớp", 409);
            wallet.PendingBalance -= pending; WalletLedger.Entry(store, wallet, "PENDING_DEBIT", -pending, wallet.PendingBalance, "Refund", refundId);
            var available = Math.Min(wallet.AvailableBalance, approved); wallet.AvailableBalance -= available;
            WalletLedger.Entry(store, wallet, "DEBIT", -available, wallet.AvailableBalance, "Refund", refundId);
            if (available < approved) { wallet.DebtBalance += approved - available; WalletLedger.Entry(store, wallet, "DEBT_CREDIT", approved - available, wallet.DebtBalance, "Refund", refundId); }
            wallet.UpdatedAt = DateTimeOffset.UtcNow; foreach (var commission in group) commission.Status = "REVERSED";
        }
    }

    private async Task ReverseSettlementsAsync(Guid orderId, Guid refundId, CancellationToken ct)
    {
        var settlements = await (from settlement in store.Set<SellerSettlement>() join seller in store.Set<SellerOrder>() on settlement.SellerOrderId equals seller.Id where seller.OrderId == orderId select settlement).ToListAsync(ct);
        foreach (var settlement in settlements)
        {
            if (settlement.Status == "PENDING") settlement.Status = "CANCELLED";
            else if (settlement.Status == "PAID" && settlement.NetAmount > 0) store.Set<SellerSettlementAdjustment>().Add(new SellerSettlementAdjustment { SellerSettlementId = settlement.Id, RefundId = refundId, Amount = settlement.NetAmount, RemainingAmount = settlement.NetAmount, Status = "OPEN", CreatedAt = DateTimeOffset.UtcNow });
        }
    }

    public async Task<PageResult<SettlementDto>> SettlementsAsync(Guid accountId, bool allBrands, CatalogQuery query, CancellationToken ct)
    {
        if (allBrands) await RequireAdmin(accountId, ct);
        else if (!await store.Set<AccountRole>().AnyAsync(x => x.AccountId == accountId && (x.Role == "BrandOwner" || x.Role == "BrandStaff"), ct)) throw new BusinessException("Cần quyền quản lý thương hiệu", 403);
        var source = store.Set<SellerSettlement>().AsNoTracking().Where(x => allBrands || store.Set<Brand>().Any(b => b.Id == x.BrandId && b.OwnerAccountId == accountId) || store.Set<BrandMember>().Any(m => m.BrandId == x.BrandId && m.AccountId == accountId && m.Status == 1 && (m.Role == "Owner" || m.Role == "Manager")));
        return await Page(from s in source join seller in store.Set<SellerOrder>() on s.SellerOrderId equals seller.Id orderby s.CreatedAt descending, s.Id select new SettlementDto(s.Id, seller.OrderId, s.BrandId, s.GrossAmount, s.FeeAmount, s.NetAmount, s.PaidAmount, s.Status, s.TransactionReference, s.CreatedAt, s.PaidAt), query, ct);
    }

    public async Task PaySettlementAsync(Guid accountId, Guid id, SettlementPaymentRequest input, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct); await RequireAdmin(accountId, ct);
        var settlement = await store.Set<SellerSettlement>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy đối soát", 404);
        if (settlement.Status == "PAID") { if (settlement.PaidAmount > 0 && settlement.TransactionReference != input.TransactionReference) throw new BusinessException("Mã giao dịch không khớp", 409); return; }
        var hold = config.GetValue<int>("Settlement:HoldDays");
        if (hold < 0) throw new InvalidOperationException("Settlement hold must not be negative.");
        if (settlement.Status != "PENDING" || settlement.CreatedAt.AddDays(hold) > DateTimeOffset.UtcNow) throw new BusinessException("Đối soát chưa đủ điều kiện thanh toán", 409);
        var orderId = await store.Set<SellerOrder>().Where(x => x.Id == settlement.SellerOrderId).Select(x => x.OrderId).SingleAsync(ct);
        if (!await store.Set<Order>().AnyAsync(x => x.Id == orderId && x.Status == "COMPLETED" && x.PaymentStatus == "PAID", ct)
            || await (from refund in store.Set<Refund>() join payment in store.Set<Payment>() on refund.PaymentId equals payment.Id where payment.OrderId == orderId && (refund.Status == "PENDING" || refund.Status == "APPROVED") select refund.Id).AnyAsync(ct)) throw new BusinessException("Đơn hàng đang hoàn tiền hoặc chưa đủ điều kiện đối soát", 409);
        var adjustments = await (from adjustment in store.Set<SellerSettlementAdjustment>() join original in store.Set<SellerSettlement>() on adjustment.SellerSettlementId equals original.Id where original.BrandId == settlement.BrandId && adjustment.RemainingAmount > 0 orderby adjustment.CreatedAt, adjustment.Id select adjustment).ToListAsync(ct);
        var amountToPay = settlement.NetAmount;
        foreach (var adjustment in adjustments)
        {
            var recovered = Math.Min(amountToPay, adjustment.RemainingAmount); if (recovered == 0) break;
            adjustment.RemainingAmount -= recovered; amountToPay -= recovered; if (adjustment.RemainingAmount == 0) adjustment.Status = "RECOVERED";
            store.Set<SellerSettlementRecovery>().Add(new SellerSettlementRecovery { AdjustmentId = adjustment.Id, AppliedToSettlementId = id, Amount = recovered, CreatedAt = DateTimeOffset.UtcNow });
        }
        if (amountToPay > 0 && (string.IsNullOrWhiteSpace(input.TransactionReference) || input.TransactionReference.Length > 150)) throw new BusinessException("Cần mã chuyển tiền thực tế sau khi cấn trừ công nợ");
        settlement.PaidAmount = amountToPay; settlement.TransactionReference = amountToPay == 0 ? "OFFSET:" + id : input.TransactionReference; settlement.Status = "PAID"; settlement.PaidAt = DateTimeOffset.UtcNow;
        Audit(accountId, "PAY_SELLER_SETTLEMENT", "SellerSettlement", id); await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<PageResult<SettlementAdjustmentDto>> AdjustmentsAsync(Guid accountId, CatalogQuery query, CancellationToken ct)
    {
        await RequireAdmin(accountId, ct);
        return await Page(store.Set<SellerSettlementAdjustment>().AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new SettlementAdjustmentDto(x.Id, x.SellerSettlementId, x.RefundId, x.Amount, x.RemainingAmount, x.Status, x.CreatedAt)), query, ct);
    }
}
