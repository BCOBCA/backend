namespace BCOBCA.DAL.Entities;

public sealed class ServiceRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ServiceId { get; set; }
    public Guid? UserId { get; set; }
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
