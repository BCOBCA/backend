namespace BCOBCA.DAL.Entities;

public sealed class PointExpiration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MembershipId { get; set; }
    public Guid PointTransactionId { get; set; }
    public long Points { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
