namespace BCOBCA.DAL.Entities;

public sealed class Shipment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string Carrier { get; set; } = "";
    public string TrackingCode { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset? ShippedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
}
