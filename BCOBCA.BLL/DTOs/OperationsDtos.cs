namespace BCOBCA.BLL.DTOs;

public sealed record ShipmentInput(Guid OrderId, string Carrier, string TrackingCode);
public sealed record ShipmentDto(Guid Id, string Carrier, string TrackingCode, string Status, DateTimeOffset? ShippedAt, DateTimeOffset? DeliveredAt);
public sealed record OrderHistoryDto(string OldStatus, string NewStatus, string Note, DateTimeOffset CreatedAt);
public sealed record ServiceRequestDto(Guid Id, Guid ServiceId, string ServiceName, string CustomerName, string Phone, string Email, string Description, string Status, DateTimeOffset CreatedAt);
public sealed record FeedbackReplyRequest(string Response);
