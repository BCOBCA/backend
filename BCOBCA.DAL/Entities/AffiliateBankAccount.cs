namespace BCOBCA.DAL.Entities;

public sealed class AffiliateBankAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string BankName { get; set; } = "";
    public string MaskedAccountNumber { get; set; } = "";
    public string DetailsEncrypted { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
