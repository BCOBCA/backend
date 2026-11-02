namespace BCOBCA.DAL.Entities;

public sealed class AffiliateWallet
{
    public decimal DebtBalance { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AffiliateProfileId { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal PendingBalance { get; set; }
    public string Currency { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public uint Version { get; set; }
}
