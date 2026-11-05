using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed class MemberService(ICommerceStore store) : IMemberService
{
    private static async Task<PageResult<T>> PageAsync<T>(IQueryable<T> source, CatalogQuery query, CancellationToken ct)
    {
        var (page, size) = CatalogService.Paging(query);
        return new(await source.Skip((page - 1) * size).Take(size).ToListAsync(ct), page, size, await source.CountAsync(ct));
    }

    public async Task<MembershipDto> MembershipAsync(Guid userId, CancellationToken ct)
    {
        var member = await store.Set<UserMembership>().AsNoTracking().SingleAsync(x => x.UserId == userId, ct);
        var level = MembershipLevelRules.Calculate(member.LifetimeEarnedPoints, await LoyaltyQueries.MembershipLevelsAsync(store, ct));
        var orders = store.Set<Order>().Where(x => x.UserId == userId);
        var counts = await orders.GroupBy(x => x.Status).Select(x => new { Status = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Status, x => x.Count, ct);
        var total = await orders.Where(x => x.Status == "COMPLETED").SumAsync(x => x.TotalAmount, ct);
        var today = CommerceRules.VietnamDate(DateTimeOffset.UtcNow);
        var latestCheckIn = await store.Set<DailyCheckIn>().AsNoTracking().Where(x => x.UserId == userId && x.CheckInDate >= today.AddDays(-1) && x.CheckInDate <= today).OrderByDescending(x => x.CheckInDate).FirstOrDefaultAsync(ct);
        var checkedIn = latestCheckIn?.CheckInDate == today;
        var wallet = await (from p in store.Set<AffiliateProfile>() join w in store.Set<AffiliateWallet>() on p.Id equals w.AffiliateProfileId where p.UserId == userId select w.AvailableBalance).SingleAsync(ct);
        return new(member.MemberCode, $"Lv.{level.Level}", member.AvailablePoints, member.LifetimeEarnedPoints, counts.Values.Sum(), total, checkedIn, wallet, counts, member.PointDebt, level, latestCheckIn?.StreakDays ?? 0);
    }

    public async Task<CheckInProgramDto> CheckInProgramAsync(CancellationToken ct)
    {
        var rule = await store.Set<CheckInRule>().AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        return new(rule?.RewardPoints ?? 0, rule is not null);
    }

    public async Task<CheckInDto> CheckInAsync(Guid userId, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var today = CommerceRules.VietnamDate(now);
        if (await store.Set<DailyCheckIn>().AnyAsync(x => x.UserId == userId && x.CheckInDate == today, ct)) throw new BusinessException("Bạn đã điểm danh hôm nay", 409);
        var rule = await store.Set<CheckInRule>().Where(x => x.IsActive).OrderBy(x => x.Id).FirstOrDefaultAsync(ct) ?? throw new BusinessException("Điểm danh hiện chưa mở", 409);
        var previous = await store.Set<DailyCheckIn>().Where(x => x.UserId == userId && x.CheckInDate == today.AddDays(-1)).SingleOrDefaultAsync(ct);
        var membership = await store.Set<UserMembership>().SingleAsync(x => x.UserId == userId, ct);
        var checkIn = new DailyCheckIn { UserId = userId, CheckInDate = today, RewardPoints = rule.RewardPoints, StreakDays = (previous?.StreakDays ?? 0) + 1, CreatedAt = now };
        store.Set<DailyCheckIn>().Add(checkIn);
        var debtRepaid = RewardLedger.Earn(store, membership, rule.RewardPoints, "CHECK_IN", "DailyCheckIn", checkIn.Id, "Điểm danh hằng ngày");
        await LoyaltyQueries.RefreshMembershipAsync(store, membership, ct);
        await store.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(today, rule.RewardPoints, checkIn.StreakDays, membership.AvailablePoints, debtRepaid);
    }

    public async Task<PageResult<PointDto>> PointsAsync(Guid userId, CatalogQuery query, CancellationToken ct)
    {
        var membershipId = await store.Set<UserMembership>().Where(x => x.UserId == userId).Select(x => x.Id).SingleAsync(ct);
        return await PageAsync(store.Set<PointTransaction>().AsNoTracking().Where(x => x.MembershipId == membershipId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new PointDto(x.Id, x.Type, x.Points, x.BalanceAfter, x.Description, x.CreatedAt)), query, ct);
    }

    public async Task<PageResult<VoucherDto>> VouchersAsync(Guid? userId, CatalogQuery query, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await PageAsync(store.Set<Voucher>().AsNoTracking().Where(x => (x.Status == "ACTIVE" || x.Status == "MEMBER_OFFER" && (x.PointCost > 0 || x.MinLevel > 1 || x.DiscountType == "SHIPPING")) && x.StartsAt <= now && x.ExpiresAt > now
                && (x.UsageLimit == null || (x.PointCost > 0 ? store.Set<UserVoucher>().Count(v => v.VoucherId == x.Id) : store.Set<VoucherUsage>().Count(u => u.VoucherId == x.Id && u.Status == "APPLIED")) < x.UsageLimit || store.Set<UserVoucher>().Any(v => v.UserId == userId && v.VoucherId == x.Id))
                && store.Set<VoucherUsage>().Count(u => u.VoucherId == x.Id && u.UserId == userId && u.Status == "APPLIED") < x.PerUserLimit)
            .OrderBy(x => x.ExpiresAt).ThenBy(x => x.Id).Select(x => new VoucherDto(x.Id, x.Code, x.Name, x.Description, x.DiscountType, x.DiscountValue, x.MaxDiscount, x.MinOrderAmount, x.ExpiresAt,
                store.Set<UserVoucher>().Any(v => v.UserId == userId && v.VoucherId == x.Id), x.PointCost, x.MinLevel, x.IsFlashSale)), query, ct);
    }

    public async Task ClaimAsync(Guid userId, Guid voucherId, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var voucher = await store.Set<Voucher>().SingleOrDefaultAsync(x => x.Id == voucherId && (x.Status == "ACTIVE" || x.Status == "MEMBER_OFFER" && (x.PointCost > 0 || x.MinLevel > 1 || x.DiscountType == "SHIPPING")) && x.StartsAt <= now && x.ExpiresAt > now, ct) ?? throw new BusinessException("Voucher không khả dụng", 404);
        if (await store.Set<UserVoucher>().AnyAsync(x => x.UserId == userId && x.VoucherId == voucherId, ct)) return;
        await VoucherEligibility.RequireLevelAsync(store, userId, voucher.MinLevel, ct);
        var usages = store.Set<VoucherUsage>().Where(x => x.VoucherId == voucherId && x.Status == "APPLIED");
        if (voucher.UsageLimit is not null && await usages.CountAsync(ct) >= voucher.UsageLimit || await usages.CountAsync(x => x.UserId == userId, ct) >= voucher.PerUserLimit) throw new BusinessException("Voucher đã hết lượt sử dụng", 409);
        if (voucher.PointCost > 0 && voucher.UsageLimit is not null && await store.Set<UserVoucher>().CountAsync(x => x.VoucherId == voucherId, ct) >= voucher.UsageLimit) throw new BusinessException("Ưu đãi đổi điểm đã hết lượt", 409);
        var claim = new UserVoucher { UserId = userId, VoucherId = voucherId, Status = "AVAILABLE", ClaimedAt = now };
        if (voucher.PointCost > 0)
        {
            var membership = await store.Set<UserMembership>().SingleAsync(x => x.UserId == userId, ct);
            RewardLedger.RedeemVoucher(store, membership, voucher.PointCost, claim.Id);
        }
        store.Set<UserVoucher>().Add(claim);
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task FavoriteAsync(Guid userId, Guid productId, bool value, CancellationToken ct)
    {
        if (!await store.Set<Product>().AnyAsync(x => x.Id == productId && x.Status == 1, ct)) throw new BusinessException("Không tìm thấy sản phẩm", 404);
        var favorite = await store.Set<ProductFavorite>().SingleOrDefaultAsync(x => x.UserId == userId && x.ProductId == productId, ct);
        if (value && favorite is null) store.Set<ProductFavorite>().Add(new ProductFavorite { UserId = userId, ProductId = productId, CreatedAt = DateTimeOffset.UtcNow });
        if (!value && favorite is not null) store.Set<ProductFavorite>().Remove(favorite);
        await store.SaveChangesAsync(ct);
    }

    public async Task<PageResult<CatalogItemDto>> FavoritesAsync(Guid userId, CatalogQuery query, CancellationToken ct)
        => await PageAsync((from f in store.Set<ProductFavorite>().AsNoTracking()
            join p in store.Set<Product>() on f.ProductId equals p.Id where f.UserId == userId && p.Status == 1
            orderby f.CreatedAt descending, f.Id
            select new CatalogItemDto(p.Id, p.BrandId, p.Name, p.Slug, p.Description, store.Set<ProductImage>().Where(i => i.ProductId == p.Id && i.IsPrimary).Select(i => i.ImageUrl).FirstOrDefault() ?? "", p.BasePrice, p.SalePrice, false, null, 0, true)), query, ct);

    public async Task<IReadOnlyList<AddressDto>> AddressesAsync(Guid userId, CancellationToken ct)
        => await store.Set<UserAddress>().AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.IsDefault).ThenBy(x => x.CreatedAt).Take(50)
            .Select(x => new AddressDto(x.Id, x.RecipientName, x.Phone, x.ProvinceCode, x.WardCode, x.AddressLine, x.IsDefault)).ToListAsync(ct);

    public async Task<AddressDto> SaveAddressAsync(Guid userId, Guid? id, AddressRequest input, CancellationToken ct)
    {
        await new AddressValidator().ValidateAndThrowAsync(input, ct);
        await using var tx = await store.BeginTransactionAsync(ct);
        var address = id is null ? new UserAddress { UserId = userId, CreatedAt = DateTimeOffset.UtcNow } : await store.Set<UserAddress>().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct) ?? throw new BusinessException("Không tìm thấy địa chỉ", 404);
        if (id is null && await store.Set<UserAddress>().CountAsync(x => x.UserId == userId, ct) >= 50) throw new BusinessException("Tối đa 50 địa chỉ");
        if (input.IsDefault)
        {
            foreach (var old in await store.Set<UserAddress>().Where(x => x.UserId == userId && x.IsDefault).ToListAsync(ct)) old.IsDefault = false;
        }
        address.RecipientName = input.RecipientName; address.Phone = input.Phone; address.ProvinceCode = input.ProvinceCode;
        address.WardCode = input.WardCode; address.AddressLine = input.AddressLine; address.IsDefault = input.IsDefault; address.UpdatedAt = DateTimeOffset.UtcNow;
        if (id is null) store.Set<UserAddress>().Add(address);
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(address.Id, address.RecipientName, address.Phone, address.ProvinceCode, address.WardCode, address.AddressLine, address.IsDefault);
    }

    public async Task DeleteAddressAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var address = await store.Set<UserAddress>().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct) ?? throw new BusinessException("Không tìm thấy địa chỉ", 404);
        store.Set<UserAddress>().Remove(address); await store.SaveChangesAsync(ct);
    }

    public async Task<AffiliateDto> AffiliateAsync(Guid userId, CancellationToken ct)
    {
        var affiliate = await (from p in store.Set<AffiliateProfile>().AsNoTracking() join w in store.Set<AffiliateWallet>() on p.Id equals w.AffiliateProfileId join u in store.Set<User>() on p.UserId equals u.Id where p.UserId == userId
            select new AffiliateDto(u.ReferralCode, w.AvailableBalance, w.PendingBalance, store.Set<AffiliateReferral>().Count(r => r.ReferrerUserId == userId), w.DebtBalance, null)).SingleAsync(ct);
        return affiliate with { RankInfo = await LoyaltyQueries.AffiliateRankAsync(store, userId, ct) };
    }

    public async Task<PageResult<ReferralDto>> ReferralsAsync(Guid userId, CatalogQuery query, CancellationToken ct)
        => await PageAsync((from r in store.Set<AffiliateReferral>().AsNoTracking() join u in store.Set<User>() on r.ReferredUserId equals u.Id where r.ReferrerUserId == userId orderby r.CreatedAt descending, r.Id select new ReferralDto(r.Id, u.FullName, r.Status, r.CreatedAt)), query, ct);

    public async Task<PageResult<NotificationDto>> NotificationsAsync(Guid accountId, CatalogQuery query, CancellationToken ct)
        => await PageAsync(store.Set<Notification>().AsNoTracking().Where(x => x.RecipientAccountId == accountId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new NotificationDto(x.Id, x.Title, x.Body, x.TargetType, x.TargetId, x.ReadAt, x.CreatedAt)), query, ct);

    public async Task ReadNotificationAsync(Guid accountId, Guid id, CancellationToken ct)
    {
        var notification = await store.Set<Notification>().SingleOrDefaultAsync(x => x.Id == id && x.RecipientAccountId == accountId, ct) ?? throw new BusinessException("Không tìm thấy thông báo", 404);
        notification.ReadAt ??= DateTimeOffset.UtcNow; await store.SaveChangesAsync(ct);
    }

    public async Task<Guid> FeedbackAsync(Guid userId, FeedbackRequest input, CancellationToken ct)
    {
        if (input.Type is not ("FEEDBACK" or "SUPPORT") || string.IsNullOrWhiteSpace(input.Subject) || input.Subject.Length > 200 || string.IsNullOrWhiteSpace(input.Content) || input.Content.Length > 10000) throw new BusinessException("Vui lòng kiểm tra nội dung phản hồi");
        var feedback = new Feedback { UserId = userId, Type = input.Type, Subject = input.Subject, Content = input.Content, Status = "OPEN", CreatedAt = DateTimeOffset.UtcNow };
        store.Set<Feedback>().Add(feedback); await store.SaveChangesAsync(ct); return feedback.Id;
    }

    public async Task<PageResult<FeedbackDto>> FeedbacksAsync(Guid userId, CatalogQuery query, CancellationToken ct)
        => await PageAsync(store.Set<Feedback>().AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new FeedbackDto(x.Id, x.Subject, x.Content, x.Status, x.AdminResponse, x.CreatedAt)), query, ct);

    public async Task<PageResult<QuotationDto>> QuotationsAsync(Guid userId, CatalogQuery query, CancellationToken ct)
        => await PageAsync((from q in store.Set<ServiceQuotation>().AsNoTracking() join r in store.Set<ServiceRequest>() on q.RequestId equals r.Id where r.UserId == userId orderby q.CreatedAt descending, q.Id select new QuotationDto(q.Id, q.QuoteCode, q.Amount, q.Description, q.ValidUntil, q.Status)), query, ct);
}
