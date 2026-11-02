namespace BCOBCA.DAL.Entities;

public sealed class BrandMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public Guid AccountId { get; set; }
    public string Role { get; set; } = "";
    public short Status { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}
