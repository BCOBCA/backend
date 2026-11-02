namespace BCOBCA.DAL.Entities;

public sealed class AffiliateReferral
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReferrerUserId { get; set; }
    public Guid ReferredUserId { get; set; }
    public string ReferralCode { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset? QualifiedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
