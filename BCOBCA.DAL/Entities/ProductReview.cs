namespace BCOBCA.DAL.Entities;

public sealed class ProductReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public Guid OrderItemId { get; set; }
    public short Rating { get; set; }
    public string Content { get; set; } = "";
    public short Status { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public uint Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
