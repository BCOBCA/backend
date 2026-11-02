namespace BCOBCA.DAL.Entities;

public sealed class UserMembership
{
    public long PointDebt { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid TierId { get; set; }
    public string MemberCode { get; set; } = "";
    public long AvailablePoints { get; set; }
    public long LifetimeEarnedPoints { get; set; }
    public decimal TotalQualifiedSpend { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? TierExpiresAt { get; set; }
    public uint Version { get; set; }
}
