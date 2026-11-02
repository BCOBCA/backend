namespace BCOBCA.DAL.Entities;

public sealed class SellerSettlementAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SellerSettlementId { get; set; }
    public Guid RefundId { get; set; }
    public decimal Amount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Status { get; set; } = "OPEN";
    public DateTimeOffset CreatedAt { get; set; }
    public uint Version { get; set; }
}
