using System.Security.Cryptography;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BCOBCA.BLL.Services;

public sealed class AffiliateLinkService(ICommerceStore store, IConfiguration config) : IAffiliateLinkService
{
    private string PublicUrl(string path)
    {
        var configured = config["App:PublicUrl"];
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var root) || root.Scheme is not ("https" or "http") || root.UserInfo != "" || root.Query != "" || root.Fragment != "")
            throw new BusinessException("Chưa cấu hình domain website chia sẻ", 503);
        return configured!.TrimEnd('/') + path;
    }

    private async Task<AffiliateProfile> Profile(Guid userId, CancellationToken ct)
        => await store.Set<AffiliateProfile>().SingleOrDefaultAsync(x => x.UserId == userId && x.Status == "ACTIVE", ct) ?? throw new BusinessException("Affiliate chưa hoạt động", 409);

    public async Task<AffiliateInvitationDto> InvitationAsync(Guid userId, CancellationToken ct)
    {
        await Profile(userId, ct);
        var code = await store.Set<User>().Where(x => x.Id == userId).Select(x => x.ReferralCode).SingleAsync(ct);
        return new(code, PublicUrl("/register?ref=" + Uri.EscapeDataString(code)), await store.Set<AffiliateReferral>().CountAsync(x => x.ReferrerUserId == userId, ct), await LoyaltyQueries.SuccessfulReferralsAsync(store, userId, ct));
    }

    public async Task<AffiliateLinkDto> CreateAsync(Guid userId, Guid productId, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct);
        var profile = await Profile(userId, ct);
        if (!await store.Set<Product>().AnyAsync(x => x.Id == productId && x.Status == 1 && x.AffiliateEnabled, ct)) throw new BusinessException("Sản phẩm không hỗ trợ Affiliate", 404);
        var link = await store.Set<AffiliateLink>().Where(x => x.AffiliateProfileId == profile.Id && x.ProductId == productId).OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (link is null)
        {
            link = new AffiliateLink { AffiliateProfileId = profile.Id, ProductId = productId, Code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)), CreatedAt = DateTimeOffset.UtcNow };
            store.Set<AffiliateLink>().Add(link);
        }
        var referralCode = await store.Set<User>().Where(x => x.Id == userId).Select(x => x.ReferralCode).SingleAsync(ct);
        var url = PublicUrl($"/product/{productId}?aff={Uri.EscapeDataString(link.Code)}&ref={Uri.EscapeDataString(referralCode)}");
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(link.Id, productId, link.Code, url, await store.Set<AffiliateVisit>().CountAsync(x => x.AffiliateLinkId == link.Id, ct));
    }

    public async Task<IReadOnlyList<AffiliateLinkDto>> LinksAsync(Guid userId, CancellationToken ct)
    {
        var profile = await Profile(userId, ct);
        var referralCode = await store.Set<User>().Where(x => x.Id == userId).Select(x => x.ReferralCode).SingleAsync(ct);
        var links = await store.Set<AffiliateLink>().Where(x => x.AffiliateProfileId == profile.Id && x.ProductId != null).OrderByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new { x.Id, x.ProductId, x.Code, Clicks = store.Set<AffiliateVisit>().Count(v => v.AffiliateLinkId == x.Id) }).ToListAsync(ct);
        return links.Select(x => new AffiliateLinkDto(x.Id, x.ProductId!.Value, x.Code, PublicUrl($"/product/{x.ProductId}?aff={Uri.EscapeDataString(x.Code)}&ref={Uri.EscapeDataString(referralCode)}"), x.Clicks)).ToArray();
    }

    public async Task<AffiliateVisitDto> TrackAsync(string code, string? previousToken, Guid? userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 50) throw new BusinessException("Link Affiliate không hợp lệ");
        if (previousToken is { Length: > 96 }) throw new BusinessException("Nguồn Affiliate không hợp lệ");
        await using var tx = await store.BeginTransactionAsync(ct);
        code = code.Trim().ToUpperInvariant();
        var now = DateTimeOffset.UtcNow;
        var days = config.GetValue<int?>("Affiliate:AttributionDays") ?? 30;
        if (days is < 1 or > 90) throw new BusinessException("Thời hạn Affiliate chưa được cấu hình hợp lệ", 503);
        var source = await (from link in store.Set<AffiliateLink>() join profile in store.Set<AffiliateProfile>() on link.AffiliateProfileId equals profile.Id
            join owner in store.Set<User>() on profile.UserId equals owner.Id join account in store.Set<Account>() on owner.AccountId equals account.Id
            join product in store.Set<Product>() on link.ProductId equals product.Id
            where link.Code == code && profile.Status == "ACTIVE" && account.Status == 1 && product.Status == 1 && product.AffiliateEnabled
            select new { Link = link, profile.UserId }).SingleOrDefaultAsync(ct) ?? throw new BusinessException("Link Affiliate không còn khả dụng", 404);
        if (source.UserId == userId) throw new BusinessException("Không thể tự giới thiệu sản phẩm của chính mình", 409);
        if (previousToken is { Length: 96 })
        {
            var hash = AffiliateSources.Hash(previousToken);
            var previous = await store.Set<AffiliateVisit>().SingleOrDefaultAsync(x => x.TokenHash == hash && x.ExpiresAt > now, ct);
            if (previous is not null && (previous.UserId is null || previous.UserId == userId))
            {
                if (previous.AffiliateLinkId == source.Link.Id && previous.CreatedAt > now.AddMinutes(-1)) { await tx.CommitAsync(ct); return new(previousToken, previous.ExpiresAt); }
                previous.ExpiresAt = now;
            }
        }
        if (userId is not null)
            foreach (var previous in await store.Set<AffiliateVisit>().Where(x => x.UserId == userId && x.ExpiresAt > now).ToListAsync(ct)) previous.ExpiresAt = now;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(48));
        var visit = new AffiliateVisit { AffiliateLinkId = source.Link.Id, TokenHash = AffiliateSources.Hash(token), UserId = userId, CreatedAt = now, ExpiresAt = now.AddDays(days) };
        store.Set<AffiliateVisit>().Add(visit); await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(token, visit.ExpiresAt);
    }

    private async Task RequireAdmin(Guid accountId, CancellationToken ct)
    {
        if (!await store.Set<Account>().AnyAsync(x => x.Id == accountId && x.Status == 1, ct) || !await store.Set<AccountRole>().AnyAsync(x => x.AccountId == accountId && (x.Role == "Admin" || x.Role == "SuperAdmin"), ct)) throw new BusinessException("Cần quyền quản trị", 403);
    }

    public async Task<ProductAffiliatePolicyRequest> ProductPolicyAsync(Guid accountId, Guid productId, CancellationToken ct)
    {
        await RequireAdmin(accountId, ct);
        return await store.Set<Product>().Where(x => x.Id == productId).Select(x => new ProductAffiliatePolicyRequest(x.AffiliateEnabled, x.AffiliateMaxRate, x.AffiliateBudget)).SingleOrDefaultAsync(ct) ?? throw new BusinessException("Không tìm thấy sản phẩm", 404);
    }

    public async Task SetProductPolicyAsync(Guid accountId, Guid productId, ProductAffiliatePolicyRequest input, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct); await RequireAdmin(accountId, ct);
        if (input.MaxRate is < 0 or > 1 || input.MaxRate is not null && decimal.Round(input.MaxRate.Value, 4) != input.MaxRate || input.Budget is < 0 or > 9999999999999999.99m || input.Budget is not null && decimal.Round(input.Budget.Value, 2) != input.Budget) throw new BusinessException("Giới hạn hoa hồng không hợp lệ");
        var product = await store.Set<Product>().SingleOrDefaultAsync(x => x.Id == productId, ct) ?? throw new BusinessException("Không tìm thấy sản phẩm", 404);
        product.AffiliateEnabled = input.Enabled; product.AffiliateMaxRate = input.MaxRate; product.AffiliateBudget = input.Budget;
        store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, Action = "UPDATE_PRODUCT_AFFILIATE_POLICY", EntityType = "Product", EntityId = productId, CreatedAt = DateTimeOffset.UtcNow });
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
