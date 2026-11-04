namespace BCOBCA.BLL.DTOs;

public sealed record CartItemRequest(Guid ProductId, Guid VariantId, int Quantity);
public sealed record UpdateCartRequest(int Quantity, bool IsSelected);
public sealed record CartItemDto(Guid Id, Guid ProductId, Guid VariantId, Guid BrandId, string BrandName, string Name, string VariantName, string ImageUrl, decimal UnitPrice, int Quantity, int AvailableStock, bool IsSelected, bool IsAvailable);
public sealed record CartDto(IReadOnlyList<CartItemDto> Items, decimal SelectedTotal, int SelectedCount);
public sealed record CheckoutRequest(Guid AddressId, string PaymentMethod = "COD", string? VoucherCode = null, string Note = "", decimal? ExpectedTotal = null, string? ExpectedCartHash = null, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? AffiliateVisitToken = null);
public sealed record CheckoutPreviewRequest(string? VoucherCode = null);
public sealed record CheckoutPreviewDto(decimal Subtotal, decimal DiscountAmount, decimal ShippingFee, decimal TotalAmount, int SelectedCount, string CartHash);
public sealed record OrderItemDto(Guid Id, Guid ProductId, Guid BrandId, string ProductName, string SKU, int Quantity, decimal UnitPrice, decimal DiscountAmount, decimal TotalPrice);
public sealed record OrderDto(Guid Id, string OrderCode, string Status, string PaymentStatus, decimal Subtotal, decimal DiscountAmount, decimal ShippingFee, decimal TotalAmount, string RecipientName, string RecipientPhone, string ShippingAddress, DateTimeOffset CreatedAt, IReadOnlyList<OrderItemDto> Items, IReadOnlyList<ShipmentDto>? Shipments = null, IReadOnlyList<OrderHistoryDto>? History = null);
public sealed record OrderSummaryDto(Guid Id, string OrderCode, string Status, string PaymentStatus, decimal TotalAmount, DateTimeOffset CreatedAt);
public sealed record OrderStatusRequest(string Status, bool ConfirmCodCollected = false);
