namespace BCOBCA.DAL.Entities;

public sealed class AffiliateProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string AffiliateCode { get; set; } = "";
    public string Status { get; set; } = "";
    public int TotalReferrals { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}
