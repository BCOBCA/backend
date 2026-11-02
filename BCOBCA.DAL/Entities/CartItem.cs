namespace BCOBCA.DAL.Entities;

public sealed class CartItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CartId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; }
    public bool IsSelected { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
