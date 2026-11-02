namespace BCOBCA.DAL.Entities;

public sealed class AffiliateWalletTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WalletId { get; set; }
    public string Type { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string ReferenceType { get; set; } = "";
    public Guid ReferenceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
