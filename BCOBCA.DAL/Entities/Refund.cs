namespace BCOBCA.DAL.Entities;

public sealed class Refund
{
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public bool RestockReturnedItems { get; set; }
    public uint Version { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "";
    public string ProviderRefundCode { get; set; } = "";
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
