namespace BCOBCA.DAL.Entities;
public sealed class PushDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationId { get; set; }
    public Guid DeviceTokenId { get; set; }
    public string Status { get; set; } = "PENDING";
    public string? TicketId { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
}
