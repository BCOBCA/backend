namespace BCOBCA.DAL.Entities;
public sealed class ShipmentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShipmentId { get; set; }
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
