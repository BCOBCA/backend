namespace BCOBCA.DAL.Entities;

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderCode { get; set; } = "";
    public Guid UserId { get; set; }
    public Guid? CheckoutGroupId { get; set; }
    public string Status { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal ShippingDiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string RecipientName { get; set; } = "";
    public string RecipientPhone { get; set; } = "";
    public string ShippingAddress { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public uint Version { get; set; }
}
