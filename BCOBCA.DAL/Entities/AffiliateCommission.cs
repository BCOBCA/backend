namespace BCOBCA.DAL.Entities;

public sealed class AffiliateCommission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AffiliateProfileId { get; set; }
    public Guid OrderItemId { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string RankName { get; set; } = "";
    public int? Rank { get; set; }
    public decimal PaidAmount { get; set; }
    public uint Version { get; set; }
}
