namespace BCOBCA.DAL.Entities;

public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AccountId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public Guid? EntityId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
