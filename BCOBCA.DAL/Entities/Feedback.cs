namespace BCOBCA.DAL.Entities;

public sealed class Feedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Type { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Content { get; set; } = "";
    public string Status { get; set; } = "";
    public string AdminResponse { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
