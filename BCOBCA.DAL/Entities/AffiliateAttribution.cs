namespace BCOBCA.DAL.Entities;

public sealed class AffiliateAttribution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AffiliateProfileId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? AffiliateLinkId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
