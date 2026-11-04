using BCOBCA.BLL.DTOs;
namespace BCOBCA.BLL.Interfaces;

public interface IAffiliateLinkService
{
    Task<AffiliateInvitationDto> InvitationAsync(Guid userId, CancellationToken ct);
    Task<AffiliateLinkDto> CreateAsync(Guid userId, Guid productId, CancellationToken ct);
    Task<IReadOnlyList<AffiliateLinkDto>> LinksAsync(Guid userId, CancellationToken ct);
    Task<AffiliateVisitDto> TrackAsync(string code, string? previousToken, Guid? userId, CancellationToken ct);
    Task<ProductAffiliatePolicyRequest> ProductPolicyAsync(Guid accountId, Guid productId, CancellationToken ct);
    Task SetProductPolicyAsync(Guid accountId, Guid productId, ProductAffiliatePolicyRequest input, CancellationToken ct);
}
