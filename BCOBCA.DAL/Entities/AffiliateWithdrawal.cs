namespace BCOBCA.DAL.Entities;

public sealed class AffiliateWithdrawal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WalletId { get; set; }
    public decimal Amount { get; set; }
    public string BankName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountHolder { get; set; } = "";
    public string Status { get; set; } = "";
    public string TransactionReference { get; set; } = "";
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public uint Version { get; set; }
    public string BankDetailsEncrypted { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
}
