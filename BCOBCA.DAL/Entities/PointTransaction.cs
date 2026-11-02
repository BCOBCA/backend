namespace BCOBCA.DAL.Entities;

public sealed class PointTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MembershipId { get; set; }
    public string Type { get; set; } = "";
    public long Points { get; set; }
    public long BalanceAfter { get; set; }
    public string ReferenceType { get; set; } = "";
    public Guid? ReferenceId { get; set; }
    public string Description { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
