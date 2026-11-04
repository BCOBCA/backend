namespace BCOBCA.DAL.Entities;

public sealed class AffiliateVisit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AffiliateLinkId { get; set; }
    public Guid? UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
