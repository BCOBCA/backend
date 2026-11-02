namespace BCOBCA.DAL.Entities;

public sealed class InventoryReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VariantId { get; set; }
    public Guid OrderId { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
