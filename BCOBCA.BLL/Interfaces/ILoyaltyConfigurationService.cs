using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface ILoyaltyConfigurationService
{
    Task<IReadOnlyList<MembershipLevelDto>> MembershipLevelsAsync(CancellationToken ct);
    Task UpdateMembershipLevelsAsync(Guid accountId, UpdateMembershipLevelsRequest input, CancellationToken ct);
    Task<IReadOnlyList<AffiliateRankDto>> AffiliateRanksAsync(CancellationToken ct);
    Task UpdateAffiliateRanksAsync(Guid accountId, UpdateAffiliateRanksRequest input, CancellationToken ct);
}
