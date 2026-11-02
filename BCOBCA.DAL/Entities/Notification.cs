namespace BCOBCA.DAL.Entities;

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipientAccountId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string TargetType { get; set; } = "";
    public Guid? TargetId { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
