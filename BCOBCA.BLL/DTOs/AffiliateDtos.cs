namespace BCOBCA.BLL.DTOs;

public sealed record AffiliateLinkRequest(Guid ProductId);
public sealed record AffiliateLinkDto(Guid Id, Guid ProductId, string Code, string Url, int Clicks);
public sealed record AffiliateVisitRequest(string Code, string? PreviousToken = null);
public sealed record AffiliateVisitDto(string Token, DateTimeOffset ExpiresAt);
public sealed record AffiliateInvitationDto(string Code, string Url, int TotalInvites, int SuccessfulInvites);
public sealed record ProductAffiliatePolicyRequest(bool Enabled = true, decimal? MaxRate = null, decimal? Budget = null);
