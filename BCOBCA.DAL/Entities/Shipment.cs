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
    public int? GhnShopId { get; set; }
    public string? RequestJson { get; set; }
    public DateTimeOffset? ProviderUpdatedAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public int CreationAttempts { get; set; }
}
