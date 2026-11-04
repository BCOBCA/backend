namespace BCOBCA.DAL.Entities;

public sealed class AffiliateRank
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Rank { get; set; }
    public string Name { get; set; } = "";
    public int MinSuccessfulReferrals { get; set; }
    public decimal CommissionRate { get; set; }
}
