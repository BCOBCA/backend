using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BCOBCA.BLL.Services;

public sealed class FinanceService(ICommerceStore store, IConfiguration config) : IFinanceService
{
    private async Task<AffiliateWallet> Wallet(Guid userId, CancellationToken ct) => await (from p in store.Set<AffiliateProfile>() join w in store.Set<AffiliateWallet>() on p.Id equals w.AffiliateProfileId where p.UserId == userId && p.Status == "ACTIVE" select w).SingleOrDefaultAsync(ct) ?? throw new BusinessException("Affiliate chưa hoạt động", 409);
    private async Task RequireAdmin(Guid accountId, CancellationToken ct)
    {
        if (!await store.Set<AccountRole>().AnyAsync(x => x.AccountId == accountId && (x.Role == "Admin" || x.Role == "SuperAdmin"), ct)) throw new BusinessException("Không có quyền quản trị tài chính", 403);
    }
    private void Ledger(AffiliateWallet wallet, string type, decimal amount, decimal balance, string referenceType, Guid id)
        => WalletLedger.Entry(store, wallet, type, amount, balance, referenceType, id);
    private void Audit(Guid accountId, string action, string entity, Guid id)
        => store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, Action = action, EntityType = entity, EntityId = id, CreatedAt = DateTimeOffset.UtcNow });
    private static async Task<PageResult<T>> Page<T>(IQueryable<T> source, CatalogQuery query, CancellationToken ct)
    {
        var (page, size) = CatalogService.Paging(query);
        return new(await source.Skip((page - 1) * size).Take(size).ToListAsync(ct), page, size, await source.CountAsync(ct));
    }

    public async Task AccrueOrderAsync(Guid orderId, CancellationToken ct)
    {
        if (!store.HasTransaction) throw new InvalidOperationException("Order accrual requires the order transaction.");
        var order = await store.Set<Order>().SingleAsync(x => x.Id == orderId, ct);
        if (order.Status != "COMPLETED" || order.PaymentStatus != "PAID") throw new BusinessException("Chỉ tích lũy cho đơn hoàn thành đã thanh toán", 409);
        var membership = await store.Set<UserMembership>().SingleAsync(x => x.UserId == order.UserId, ct);
        if (await store.Set<PointTransaction>().AnyAsync(x => x.MembershipId == membership.Id && x.Type == "ORDER_EARN" && x.ReferenceId == orderId, ct)) return;
        var spend = order.Subtotal - order.DiscountAmount;
        var spendPerPoint = config.GetValue<decimal>("Membership:SpendPerPoint");
        if (spendPerPoint < 0) throw new InvalidOperationException("Membership spend-per-point must not be negative.");
        var points = spendPerPoint > 0 ? checked((long)decimal.Floor(spend / spendPerPoint)) : 0;
        RewardLedger.Earn(store, membership, points, "ORDER_EARN", "Order", orderId, "Tích lũy đơn " + order.OrderCode);
        membership.TotalQualifiedSpend += spend;
        var tier = await store.Set<MembershipTier>().Where(x => x.IsActive && x.MinSpend <= membership.TotalQualifiedSpend && x.MinPoints <= membership.LifetimeEarnedPoints).OrderByDescending(x => x.SortOrder).FirstOrDefaultAsync(ct);
        if (tier is not null) membership.TierId = tier.Id;
        var rate = config.GetValue<decimal>("Affiliate:CommissionRate");
        if (rate < 0 || rate > 1 || decimal.Round(rate, 4) != rate) throw new InvalidOperationException("Affiliate commission rate must be between 0 and 1 with at most 4 decimal places.");
        var referredBy = await store.Set<User>().Where(x => x.Id == order.UserId).Select(x => x.ReferredByUserId).SingleAsync(ct);
        if (referredBy is not null)
        {
            var referral = await store.Set<AffiliateReferral>().SingleOrDefaultAsync(x => x.ReferredUserId == order.UserId, ct);
            if (referral is not null) { referral.Status = "QUALIFIED"; referral.QualifiedAt ??= DateTimeOffset.UtcNow; }
            var profile = await store.Set<AffiliateProfile>().SingleOrDefaultAsync(x => x.UserId == referredBy && x.Status == "ACTIVE", ct);
            if (profile is not null && rate > 0)
            {
                var wallet = await store.Set<AffiliateWallet>().SingleAsync(x => x.AffiliateProfileId == profile.Id, ct);
                if (!await store.Set<AffiliateAttribution>().AnyAsync(x => x.OrderId == orderId, ct)) store.Set<AffiliateAttribution>().Add(new AffiliateAttribution { AffiliateProfileId = profile.Id, OrderId = orderId, CreatedAt = DateTimeOffset.UtcNow });
                foreach (var line in await store.Set<OrderItem>().Where(x => x.OrderId == orderId).OrderBy(x => x.Id).ToListAsync(ct))
                {
                    var amount = decimal.Round(line.TotalPrice * rate, 2, MidpointRounding.AwayFromZero);
                    if (amount == 0) continue;
                    var commission = new AffiliateCommission { AffiliateProfileId = profile.Id, OrderItemId = line.Id, CommissionRate = rate, BaseAmount = line.TotalPrice, CommissionAmount = amount, Status = "PENDING", CreatedAt = DateTimeOffset.UtcNow };
                    store.Set<AffiliateCommission>().Add(commission); wallet.PendingBalance += amount;
                    Ledger(wallet, "PENDING_CREDIT", amount, wallet.PendingBalance, "Commission", commission.Id);
                }
                wallet.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        var feeRate = config.GetValue<decimal>("Settlement:PlatformFeeRate");
        if (feeRate < 0 || feeRate > 1 || decimal.Round(feeRate, 4) != feeRate) throw new InvalidOperationException("Settlement platform fee rate is invalid.");
        foreach (var sellerOrder in await store.Set<SellerOrder>().Where(x => x.OrderId == orderId).ToListAsync(ct))
        {
            var fee = decimal.Round(sellerOrder.TotalAmount * feeRate, 2, MidpointRounding.AwayFromZero);
            store.Set<SellerSettlement>().Add(new SellerSettlement { BrandId = sellerOrder.BrandId, SellerOrderId = sellerOrder.Id, GrossAmount = sellerOrder.TotalAmount, FeeAmount = fee, NetAmount = sellerOrder.TotalAmount - fee, Status = "PENDING", CreatedAt = DateTimeOffset.UtcNow });
        }
        await store.SaveChangesAsync(ct);
    }

    private byte[] BankKey()
    {
        try
        {
            var key = Convert.FromBase64String(config["Security:BankDataKey"] ?? "");
            if (key.Length != 32) throw new FormatException(); return key;
        }
        catch (FormatException) { throw new BusinessException("Chưa cấu hình khóa mã hóa thông tin ngân hàng", 503); }
    }
    private string Encrypt(BankDetailsDto details)
    {
        var nonce = RandomNumberGenerator.GetBytes(12); var plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(details)); var encrypted = new byte[plain.Length]; var tag = new byte[16];
        using var aes = new AesGcm(BankKey(), 16); aes.Encrypt(nonce, plain, encrypted, tag);
        return "v1:" + Convert.ToBase64String(nonce.Concat(tag).Concat(encrypted).ToArray());
    }
    private BankDetailsDto Decrypt(string value)
    {
        if (!value.StartsWith("v1:", StringComparison.Ordinal)) throw new BusinessException("Không thể đọc thông tin ngân hàng", 409);
        var data = Convert.FromBase64String(value[3..]);
        if (data.Length <= 28) throw new BusinessException("Không thể đọc thông tin ngân hàng", 409);
        var plain = new byte[data.Length - 28]; using var aes = new AesGcm(BankKey(), 16); aes.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), plain);
        return JsonSerializer.Deserialize<BankDetailsDto>(plain) ?? throw new InvalidOperationException("Invalid bank payload.");
    }

    public async Task<Guid> WithdrawAsync(Guid userId, string idempotencyKey, WithdrawalRequest input, CancellationToken ct)
    {
        if (input.Amount <= 0 || input.Amount > 9999999999999999.99m || decimal.Round(input.Amount, 2) != input.Amount || string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100 || string.IsNullOrWhiteSpace(input.BankName) || input.BankName.Length > 150 || string.IsNullOrWhiteSpace(input.AccountHolder) || input.AccountHolder.Length > 150 || !Regex.IsMatch(input.AccountNumber, "^[0-9]{6,34}$")) throw new BusinessException("Thông tin rút tiền không hợp lệ");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
        await using var tx = await store.BeginTransactionAsync(ct); var wallet = await Wallet(userId, ct);
        var existing = await store.Set<AffiliateWithdrawal>().SingleOrDefaultAsync(x => x.WalletId == wallet.Id && x.IdempotencyKey == idempotencyKey, ct);
        if (existing is not null) { if (existing.RequestHash != hash) throw new BusinessException("Mã yêu cầu đã dùng cho nội dung khác", 409); return existing.Id; }
        if (wallet.DebtBalance > 0) throw new BusinessException("Cần cấn trừ hoa hồng đã thu hồi trước khi rút tiền", 409);
        if (wallet.AvailableBalance < input.Amount) throw new BusinessException("Số dư không đủ", 409);
        var withdrawal = new AffiliateWithdrawal { WalletId = wallet.Id, Amount = input.Amount, BankName = input.BankName, AccountNumber = "****" + input.AccountNumber[^4..], AccountHolder = input.AccountHolder[..1] + "***",
            BankDetailsEncrypted = Encrypt(new(input.BankName, input.AccountNumber, input.AccountHolder)), IdempotencyKey = idempotencyKey, RequestHash = hash, Status = "PENDING", RequestedAt = DateTimeOffset.UtcNow };
        wallet.AvailableBalance -= input.Amount; wallet.UpdatedAt = DateTimeOffset.UtcNow;
        store.Set<AffiliateWithdrawal>().Add(withdrawal); Ledger(wallet, "DEBIT", -input.Amount, wallet.AvailableBalance, "Withdrawal", withdrawal.Id);
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct); return withdrawal.Id;
    }

    public async Task<PageResult<WithdrawalDto>> WithdrawalsAsync(Guid userId, CatalogQuery query, CancellationToken ct)
    {
        var wallet = await Wallet(userId, ct);
        return await Page(store.Set<AffiliateWithdrawal>().AsNoTracking().Where(x => x.WalletId == wallet.Id).OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id)
            .Select(x => new WithdrawalDto(x.Id, x.Amount, x.BankName, x.AccountNumber, x.AccountHolder, x.Status, x.TransactionReference, x.RequestedAt, x.ProcessedAt)), query, ct);
    }
    public async Task<PageResult<WalletTransactionDto>> TransactionsAsync(Guid userId, CatalogQuery query, CancellationToken ct)
    {
        var wallet = await Wallet(userId, ct);
        return await Page(store.Set<AffiliateWalletTransaction>().AsNoTracking().Where(x => x.WalletId == wallet.Id).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new WalletTransactionDto(x.Id, x.Type, x.Amount, x.BalanceAfter, x.ReferenceType, x.ReferenceId, x.CreatedAt)), query, ct);
    }
    public async Task<PageResult<CommissionDto>> CommissionsAsync(Guid userId, CatalogQuery query, CancellationToken ct)
    {
        var profileId = await store.Set<AffiliateProfile>().Where(x => x.UserId == userId).Select(x => x.Id).SingleAsync(ct);
        return await Page(store.Set<AffiliateCommission>().AsNoTracking().Where(x => x.AffiliateProfileId == profileId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new CommissionDto(x.Id, x.OrderItemId, x.BaseAmount, x.CommissionRate, x.CommissionAmount, x.Status, x.CreatedAt, x.ApprovedAt)), query, ct);
    }
    public async Task<PageResult<LeaderboardDto>> LeaderboardAsync(CatalogQuery query, CancellationToken ct)
    {
        var (page, size) = CatalogService.Paging(query);
        var source = from p in store.Set<AffiliateProfile>().AsNoTracking() join u in store.Set<User>() on p.UserId equals u.Id where p.Status == "ACTIVE"
            select new { p.Id, DisplayName = u.Nickname == "" ? "Thành viên " + p.AffiliateCode.Substring(0, 6) : u.Nickname, Count = store.Set<AffiliateReferral>().Count(r => r.ReferrerUserId == u.Id) };
        var items = await source.OrderByDescending(x => x.Count).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(items.Select((x, i) => new LeaderboardDto(x.Id, x.DisplayName, x.Count, (page - 1) * size + i + 1)).ToArray(), page, size, await source.CountAsync(ct));
    }

    public async Task ApproveCommissionAsync(Guid accountId, Guid id, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct); await RequireAdmin(accountId, ct);
        var commission = await store.Set<AffiliateCommission>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy hoa hồng", 404);
        if (commission.Status == "APPROVED") return;
        var orderId = await store.Set<OrderItem>().Where(x => x.Id == commission.OrderItemId).Select(x => x.OrderId).SingleAsync(ct);
        if (!await store.Set<Order>().AnyAsync(x => x.Id == orderId && x.Status == "COMPLETED" && x.PaymentStatus == "PAID", ct)
            || await (from refund in store.Set<Refund>() join payment in store.Set<Payment>() on refund.PaymentId equals payment.Id where payment.OrderId == orderId && (refund.Status == "PENDING" || refund.Status == "APPROVED") select refund.Id).AnyAsync(ct)) throw new BusinessException("Đơn hàng đang hoàn tiền hoặc chưa đủ điều kiện duyệt hoa hồng", 409);
        var holdDays = config.GetValue<int>("Affiliate:ApprovalHoldDays");
        if (holdDays < 0) throw new InvalidOperationException("Approval hold must not be negative.");
        if (commission.Status != "PENDING" || commission.CreatedAt.AddDays(holdDays) > DateTimeOffset.UtcNow) throw new BusinessException("Hoa hồng chưa đủ điều kiện duyệt", 409);
        var wallet = await store.Set<AffiliateWallet>().SingleAsync(x => x.AffiliateProfileId == commission.AffiliateProfileId, ct);
        if (wallet.PendingBalance < commission.CommissionAmount) throw new BusinessException("Số dư chờ duyệt không khớp sổ cái", 409);
        wallet.PendingBalance -= commission.CommissionAmount; wallet.UpdatedAt = DateTimeOffset.UtcNow;
        commission.Status = "APPROVED"; commission.ApprovedAt = DateTimeOffset.UtcNow;
        Ledger(wallet, "PENDING_DEBIT", -commission.CommissionAmount, wallet.PendingBalance, "Commission", id);
        WalletLedger.CreditAvailable(store, wallet, commission.CommissionAmount, "Commission", id); Audit(accountId, "APPROVE_COMMISSION", "AffiliateCommission", id);
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task SetWithdrawalStatusAsync(Guid accountId, Guid id, WithdrawalStatusRequest input, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct); await RequireAdmin(accountId, ct);
        var withdrawal = await store.Set<AffiliateWithdrawal>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy yêu cầu rút tiền", 404);
        if (withdrawal.Status == input.Status) { if (input.Status == "PAID" && withdrawal.TransactionReference != input.TransactionReference) throw new BusinessException("Mã giao dịch không khớp", 409); return; }
        if (input.Status == "APPROVED" && withdrawal.Status == "PENDING") withdrawal.Status = "APPROVED";
        else if (input.Status == "PAID" && withdrawal.Status == "APPROVED")
        {
            var payoutWallet = await store.Set<AffiliateWallet>().SingleAsync(x => x.Id == withdrawal.WalletId, ct);
            if (payoutWallet.DebtBalance > 0) throw new BusinessException("Cần đối soát hoa hồng bị thu hồi trước khi xác nhận chi", 409);
            if (string.IsNullOrWhiteSpace(input.TransactionReference) || input.TransactionReference.Length > 150) throw new BusinessException("Cần mã xác nhận chuyển tiền thực tế");
            withdrawal.Status = "PAID"; withdrawal.TransactionReference = input.TransactionReference; withdrawal.ProcessedAt = DateTimeOffset.UtcNow;
        }
        else if (input.Status == "REJECTED" && withdrawal.Status is "PENDING" or "APPROVED")
        {
            var wallet = await store.Set<AffiliateWallet>().SingleAsync(x => x.Id == withdrawal.WalletId, ct);
            WalletLedger.CreditAvailable(store, wallet, withdrawal.Amount, "Withdrawal", id); withdrawal.Status = "REJECTED"; withdrawal.ProcessedAt = DateTimeOffset.UtcNow;
        }
        else throw new BusinessException("Chuyển trạng thái rút tiền không hợp lệ", 409);
        Audit(accountId, "WITHDRAWAL_" + input.Status, "AffiliateWithdrawal", id); await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<BankDetailsDto> BankDetailsAsync(Guid accountId, Guid id, CancellationToken ct)
    {
        await RequireAdmin(accountId, ct);
        var withdrawal = await store.Set<AffiliateWithdrawal>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy yêu cầu rút tiền", 404);
        var details = Decrypt(withdrawal.BankDetailsEncrypted);
        Audit(accountId, "VIEW_BANK_DETAILS", "AffiliateWithdrawal", id); await store.SaveChangesAsync(ct); return details;
    }
}
