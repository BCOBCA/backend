namespace BCOBCA.DAL.Entities;

public sealed class ServiceQuotation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public string QuoteCode { get; set; } = "";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public DateTimeOffset ValidUntil { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
