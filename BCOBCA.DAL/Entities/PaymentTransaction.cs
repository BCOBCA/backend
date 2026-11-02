namespace BCOBCA.DAL.Entities;

public sealed class PaymentTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public string Provider { get; set; } = "";
    public string TransactionCode { get; set; } = "";
    public string EventId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public string ResponseData { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}
