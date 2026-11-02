namespace BCOBCA.DAL.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string TokenHash { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid FamilyId { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public uint Version { get; set; }
}
