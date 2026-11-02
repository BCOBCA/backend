namespace BCOBCA.DAL.Entities;

public sealed class SellerOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid BrandId { get; set; }
    public string Status { get; set; } = "";
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
