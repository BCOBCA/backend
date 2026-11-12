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
        await LoyaltyQueries.RefreshMembershipAsync(store, membership, ct);
        var referredBy = await store.Set<User>().Where(x => x.Id == order.UserId).Select(x => x.ReferredByUserId).SingleAsync(ct);
        if (referredBy is not null)
        {
            var referral = await store.Set<AffiliateReferral>().SingleOrDefaultAsync(x => x.ReferredUserId == order.UserId, ct);
            if (referral is not null) { referral.Status = "QUALIFIED"; referral.QualifiedAt ??= DateTimeOffset.UtcNow; }
        }
        var attribution = await store.Set<AffiliateAttribution>().SingleOrDefaultAsync(x => x.OrderId == orderId, ct);
        var source = attribution is null
            ? store.Set<AffiliateProfile>().Where(x => x.UserId == referredBy)
            : store.Set<AffiliateProfile>().Where(x => x.Id == attribution.AffiliateProfileId);
        var profile = await (from candidate in source join owner in store.Set<User>() on candidate.UserId equals owner.Id join account in store.Set<Account>() on owner.AccountId equals account.Id
            where candidate.Status == "ACTIVE" && candidate.UserId != order.UserId && account.Status == 1 select candidate).SingleOrDefaultAsync(ct);
        if (profile is not null)
        {
            var rank = await LoyaltyQueries.AffiliateRankAsync(store, profile.UserId, ct);
            var linkedProduct = attribution?.AffiliateLinkId is Guid linkId ? await store.Set<AffiliateLink>().Where(x => x.Id == linkId).Select(x => x.ProductId).SingleAsync(ct) : null;
            var wallet = await store.Set<AffiliateWallet>().SingleAsync(x => x.AffiliateProfileId == profile.Id, ct);
            if (attribution is null) store.Set<AffiliateAttribution>().Add(new AffiliateAttribution { AffiliateProfileId = profile.Id, OrderId = orderId, CreatedAt = DateTimeOffset.UtcNow });
            var lines = await store.Set<OrderItem>().Where(x => x.OrderId == orderId && (linkedProduct == null || x.ProductId == linkedProduct)).OrderBy(x => x.Id).ToListAsync(ct);
            var productIds = lines.Select(x => x.ProductId).Distinct().ToArray();
            var products = await store.Set<Product>().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            var spentByProduct = await (from previous in store.Set<AffiliateCommission>() join item in store.Set<OrderItem>() on previous.OrderItemId equals item.Id
                where productIds.Contains(item.ProductId) && previous.Status != "REVERSED" group previous by item.ProductId into totals
                select new { ProductId = totals.Key, Amount = totals.Sum(x => x.CommissionAmount) }).ToDictionaryAsync(x => x.ProductId, x => x.Amount, ct);
            foreach (var line in lines)
            {
                if (!products.TryGetValue(line.ProductId, out var product) || !product.AffiliateEnabled) continue;
                var rate = Math.Min(rank.CommissionRate, product.AffiliateMaxRate ?? 1m);
                var amount = decimal.Round(line.TotalPrice * rate, 2, MidpointRounding.AwayFromZero);
                if (product.AffiliateBudget is decimal budget) amount = Math.Min(amount, Math.Max(0, budget - spentByProduct.GetValueOrDefault(product.Id)));
                if (amount == 0) continue;
                spentByProduct[product.Id] = spentByProduct.GetValueOrDefault(product.Id) + amount;
                var commission = new AffiliateCommission { AffiliateProfileId = profile.Id, OrderItemId = line.Id, CommissionRate = rate, BaseAmount = line.TotalPrice, CommissionAmount = amount, Rank = rank.Rank, RankName = rank.Name, Status = "PENDING", CreatedAt = DateTimeOffset.UtcNow };
                store.Set<AffiliateCommission>().Add(commission); wallet.PendingBalance += amount;
                Ledger(wallet, "PENDING_CREDIT", amount, wallet.PendingBalance, "Commission", commission.Id);
            }
            wallet.UpdatedAt = DateTimeOffset.UtcNow;
        }
        var feeRate = await store.Set<PlatformFeeConfiguration>().AsNoTracking()
            .Where(x => x.Id == PlatformFeeConfiguration.SingletonId).Select(x => x.PlatformFeeRate).SingleAsync(ct);
        PlatformFeeRules.Validate(feeRate);
        foreach (var sellerOrder in await store.Set<SellerOrder>().Where(x => x.OrderId == orderId).ToListAsync(ct))
        {
            var fee = decimal.Round(sellerOrder.TotalAmount * feeRate, 2, MidpointRounding.AwayFromZero);
            store.Set<SellerSettlement>().Add(new SellerSettlement { BrandId = sellerOrder.BrandId, StoreId = sellerOrder.StoreId, SellerOrderId = sellerOrder.Id, GrossAmount = sellerOrder.TotalAmount, FeeAmount = fee, NetAmount = sellerOrder.TotalAmount - fee, Status = "PENDING", CreatedAt = DateTimeOffset.UtcNow });
        }
        await store.SaveChangesAsync(ct);
    }

    private string Encrypt(BankDetailsDto details) => BankDataProtection.Encrypt(config, details);
    private BankDetailsDto Decrypt(string value) => BankDataProtection.Decrypt(config, value);
    public async Task<Guid> WithdrawAsync(Guid userId, string idempotencyKey, WithdrawalRequest input, CancellationToken ct)
    {
        if (input.Amount <= 0 || input.Amount > 9999999999999999.99m || decimal.Round(input.Amount, 2) != input.Amount || string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100) throw new BusinessException("Thông tin rút tiền không hợp lệ");
        var payload = input.BankAccountId is null && input.ExpectedConfigurationRevision is null
            ? JsonSerializer.Serialize(new { input.Amount, input.BankName, input.AccountNumber, input.AccountHolder }) : JsonSerializer.Serialize(input);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        await using var tx = await store.BeginTransactionAsync(ct); var wallet = await Wallet(userId, ct);
        var existing = await store.Set<AffiliateWithdrawal>().SingleOrDefaultAsync(x => x.WalletId == wallet.Id && x.IdempotencyKey == idempotencyKey, ct);
        if (existing is not null) { if (existing.RequestHash != hash) throw new BusinessException("Mã yêu cầu đã dùng cho nội dung khác", 409); return existing.Id; }
        var settings = await store.Set<WithdrawalConfiguration>().AsNoTracking().SingleAsync(x => x.Id == WithdrawalConfiguration.SingletonId, ct);
        if (settings.FeeAmount > 0 && input.ExpectedConfigurationRevision is null) throw new BusinessException("Vui lòng tải và xác nhận cấu hình phí rút tiền hiện tại.", 409);
        if (input.ExpectedConfigurationRevision is { } revision && revision != settings.Revision) throw new BusinessException("Phí hoặc giới hạn rút tiền vừa thay đổi. Vui lòng tải lại và kiểm tra tóm tắt yêu cầu.", 409);
        if (input.Amount < settings.MinimumAmount || input.Amount > settings.MaximumAmount) throw new BusinessException("Số tiền nằm ngoài giới hạn rút tiền hiện tại");
        if (wallet.DebtBalance > 0) throw new BusinessException("Cần cấn trừ hoa hồng đã thu hồi trước khi rút tiền", 409);
        if (wallet.AvailableBalance < input.Amount) throw new BusinessException("Số dư không đủ", 409);
        BankDetailsDto bank;
        if (input.BankAccountId is { } bankId)
        {
            var saved = await store.Set<AffiliateBankAccount>().AsNoTracking().SingleOrDefaultAsync(a => a.Id == bankId && a.UserId == userId, ct) ?? throw new BusinessException("Không tìm thấy tài khoản ngân hàng của bạn", 404);
            bank = BankDataProtection.Validate(Decrypt(saved.DetailsEncrypted));
        }
        else bank = BankDataProtection.Validate(new(input.BankName, input.AccountNumber, input.AccountHolder));
        var withdrawal = new AffiliateWithdrawal { WalletId = wallet.Id, Amount = input.Amount, FeeAmount = settings.FeeAmount, NetAmount = input.Amount - settings.FeeAmount, BankName = bank.BankName, AccountNumber = "****" + bank.AccountNumber[^4..], AccountHolder = bank.AccountHolder[..1] + "***",
            BankDetailsEncrypted = Encrypt(bank), IdempotencyKey = idempotencyKey, RequestHash = hash, Status = "PENDING", RequestedAt = DateTimeOffset.UtcNow };
        wallet.AvailableBalance -= input.Amount; wallet.UpdatedAt = DateTimeOffset.UtcNow;
        store.Set<AffiliateWithdrawal>().Add(withdrawal); Ledger(wallet, "DEBIT", -input.Amount, wallet.AvailableBalance, "Withdrawal", withdrawal.Id);
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct); return withdrawal.Id;
    }

    public async Task<PageResult<WithdrawalDto>> WithdrawalsAsync(Guid userId, CatalogQuery query, CancellationToken ct)
    {
        var wallet = await Wallet(userId, ct);
        return await Page(store.Set<AffiliateWithdrawal>().AsNoTracking().Where(x => x.WalletId == wallet.Id).OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id)
            .Select(x => new WithdrawalDto(x.Id, x.Amount, x.BankName, x.AccountNumber, x.AccountHolder, x.Status, x.TransactionReference, x.RequestedAt, x.ProcessedAt, x.FeeAmount, x.NetAmount)), query, ct);
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
        return await Page((from commission in store.Set<AffiliateCommission>().AsNoTracking() join item in store.Set<OrderItem>() on commission.OrderItemId equals item.Id join order in store.Set<Order>() on item.OrderId equals order.Id
            where commission.AffiliateProfileId == profileId orderby commission.CreatedAt descending, commission.Id
            select new CommissionDto(commission.Id, commission.OrderItemId, commission.BaseAmount, commission.CommissionRate, commission.CommissionAmount, commission.Status, commission.CreatedAt, commission.ApprovedAt, order.Id, order.OrderCode, item.ProductId, item.ProductName, commission.RankName, commission.Rank, commission.PaidAmount)), query, ct);
    }
    public async Task<LeaderboardPageDto> LeaderboardAsync(CatalogQuery query, CancellationToken ct, string period = "all", Guid? userId = null)
    {
        var range = LeaderboardPeriods.Range(period, DateTimeOffset.UtcNow);
        var (page, size) = CatalogService.Paging(query);
        var ranks = await LoyaltyQueries.AffiliateRanksAsync(store, ct);
        var qualified = store.Set<AffiliateReferral>().Where(r => store.Set<Order>().Any(o => o.UserId == r.ReferredUserId && o.Status == "COMPLETED" && o.PaymentStatus == "PAID"));
        var source = from p in store.Set<AffiliateProfile>().AsNoTracking() join u in store.Set<User>() on p.UserId equals u.Id where p.Status == "ACTIVE"
            select new { p.Id, UserId = u.Id, u.AvatarUrl, DisplayName = u.Nickname == "" ? "Thành viên " + p.AffiliateCode.Substring(0, 6) : u.Nickname,
                Total = qualified.Count(r => r.ReferrerUserId == u.Id),
                Count = qualified.Count(r => r.ReferrerUserId == u.Id && (range.Start == null || r.QualifiedAt >= range.Start && r.QualifiedAt < range.End)) };
        var items = await source.OrderByDescending(x => x.Count).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        LeaderboardDto Entry(Guid id, string name, string avatar, int count, int total, int position, bool mine)
        {
            var tier = AffiliateRankRules.Calculate(total, ranks);
            return new(id, name, count, position, tier.Name, tier.CommissionRate, avatar, tier.Rank, mine);
        }
        LeaderboardDto? myEntry = null;
        if (userId is not null)
        {
            var mine = await source.SingleOrDefaultAsync(x => x.UserId == userId, ct);
            if (mine is not null)
            {
                var position = 1 + await source.CountAsync(x => x.Count > mine.Count || x.Count == mine.Count && string.Compare(x.Id.ToString(), mine.Id.ToString()) < 0, ct);
                myEntry = Entry(mine.Id, mine.DisplayName, mine.AvatarUrl, mine.Count, mine.Total, position, true);
            }
        }
        return new(items.Select((x, i) => Entry(x.Id, x.DisplayName, x.AvatarUrl, x.Count, x.Total, (page - 1) * size + i + 1, x.UserId == userId)).ToArray(), page, size, await source.CountAsync(ct), period, myEntry);
    }

    public async Task ApproveCommissionAsync(Guid accountId, Guid id, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct); await RequireAdmin(accountId, ct);
        var commission = await store.Set<AffiliateCommission>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy hoa hồng", 404);
        if (commission.Status is "APPROVED" or "PAID") return;
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
            var remaining = withdrawal.Amount;
            foreach (var commission in await store.Set<AffiliateCommission>().Where(x => x.AffiliateProfileId == payoutWallet.AffiliateProfileId && x.Status == "APPROVED" && x.PaidAmount < x.CommissionAmount).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct))
            {
                var portion = Math.Min(remaining, commission.CommissionAmount - commission.PaidAmount);
                commission.PaidAmount += portion; remaining -= portion;
                if (commission.PaidAmount == commission.CommissionAmount) commission.Status = "PAID";
                if (remaining == 0) break;
            }
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
