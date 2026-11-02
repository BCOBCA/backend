namespace BCOBCA.DAL.Entities;

public sealed class ProductFavorite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
