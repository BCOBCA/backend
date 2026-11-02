namespace BCOBCA.DAL.Entities;

public sealed class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VariantId { get; set; }
    public Guid? OrderId { get; set; }
    public int Quantity { get; set; }
    public string Type { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
