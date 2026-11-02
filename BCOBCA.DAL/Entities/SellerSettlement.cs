namespace BCOBCA.DAL.Entities;

public sealed class SellerSettlement
{
    public decimal PaidAmount { get; set; }
    public string TransactionReference { get; set; } = "";
    public uint Version { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public Guid SellerOrderId { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
}
