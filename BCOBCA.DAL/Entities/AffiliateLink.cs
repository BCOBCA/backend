namespace BCOBCA.DAL.Entities;

public sealed class AffiliateLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AffiliateProfileId { get; set; }
    public Guid? ProductId { get; set; }
    public string Code { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
