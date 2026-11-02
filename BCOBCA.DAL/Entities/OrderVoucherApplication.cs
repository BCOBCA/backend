namespace BCOBCA.DAL.Entities;

public sealed class OrderVoucherApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid VoucherId { get; set; }
    public decimal DiscountAmount { get; set; }
}
