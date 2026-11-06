namespace BCOBCA.DAL.Entities;

public sealed class StoreFollower
{
    public Guid StoreId { get; set; }
    public Guid AccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
