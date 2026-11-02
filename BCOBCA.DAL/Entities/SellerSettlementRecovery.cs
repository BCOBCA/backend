namespace BCOBCA.DAL.Entities;

public sealed class SellerSettlementRecovery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdjustmentId { get; set; }
    public Guid AppliedToSettlementId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
