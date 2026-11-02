namespace BCOBCA.DAL.Entities;

public sealed class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string PaymentMethod { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public string Status { get; set; } = "";
    public string ProviderReference { get; set; } = "";
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public uint Version { get; set; }
}
