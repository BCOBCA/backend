using System.Text.Json;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed class LoyaltyConfigurationService(ICommerceStore store) : ILoyaltyConfigurationService
{
    public Task<IReadOnlyList<MembershipLevelDto>> MembershipLevelsAsync(CancellationToken ct) => LoyaltyQueries.MembershipLevelsAsync(store, ct);
    public Task<IReadOnlyList<AffiliateRankDto>> AffiliateRanksAsync(CancellationToken ct) => LoyaltyQueries.AffiliateRanksAsync(store, ct);

    private async Task RequireAdmin(Guid accountId, CancellationToken ct)
    {
        if (!await store.Set<Account>().AnyAsync(x => x.Id == accountId && x.Status == 1, ct)
            || !await store.Set<AccountRole>().AnyAsync(x => x.AccountId == accountId && (x.Role == "Admin" || x.Role == "SuperAdmin"), ct))
            throw new BusinessException("Cần quyền quản trị cấu hình thành viên", 403);
    }

    public async Task UpdateMembershipLevelsAsync(Guid accountId, UpdateMembershipLevelsRequest input, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct);
        await RequireAdmin(accountId, ct);
        MembershipLevelRules.Validate(input.Levels);
        var tiers = await store.Set<MembershipTier>().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ToListAsync(ct);
        if (tiers.Count != 10 || tiers.Select(x => x.SortOrder).Distinct().Count() != 10) throw new BusinessException("Cần cập nhật cấu hình 10 level trước khi chỉnh sửa", 409);
        foreach (var definition in input.Levels)
        {
            var tier = tiers.Single(x => x.SortOrder == definition.Level - 1);
            tier.Name = $"Lv.{definition.Level}";
            tier.MinPoints = definition.MinPoints;
            tier.MinSpend = 0;
            tier.Benefits = JsonSerializer.Serialize(new { items = definition.Benefits.Select(x => x.Trim()).ToArray() });
        }
        Audit(accountId, "UPDATE_MEMBERSHIP_LEVELS", "MembershipTier");
        await store.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task UpdateAffiliateRanksAsync(Guid accountId, UpdateAffiliateRanksRequest input, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct);
        await RequireAdmin(accountId, ct);
        AffiliateRankRules.Validate(input.Ranks);
        var ranks = await store.Set<AffiliateRank>().OrderBy(x => x.Rank).ToListAsync(ct);
        if (ranks.Count != 5) throw new BusinessException("Cần cập nhật cấu hình 5 hạng giới thiệu trước khi chỉnh sửa", 409);
        foreach (var definition in input.Ranks)
        {
            var rank = ranks.Single(x => x.Rank == definition.Rank);
            rank.Name = definition.Name.Trim();
            rank.MinSuccessfulReferrals = definition.MinSuccessfulReferrals;
            rank.CommissionRate = definition.CommissionRate;
        }
        Audit(accountId, "UPDATE_AFFILIATE_RANKS", "AffiliateRank");
        await store.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private void Audit(Guid accountId, string action, string entityType)
        => store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, Action = action, EntityType = entityType, CreatedAt = DateTimeOffset.UtcNow });
}
