namespace BCOBCA.DAL.Entities;

// Technical authentication table; supplements the 57 business tables.
public sealed class AccountActionToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string TokenHash { get; set; } = "";
    public string Action { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public uint Version { get; set; }
}
