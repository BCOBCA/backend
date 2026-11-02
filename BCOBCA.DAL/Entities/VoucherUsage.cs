namespace BCOBCA.DAL.Entities;

public sealed class VoucherUsage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VoucherId { get; set; }
    public Guid UserId { get; set; }
    public Guid OrderId { get; set; }
    public decimal DiscountAmount { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
