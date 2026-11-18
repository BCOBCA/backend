namespace BCOBCA.BLL.DTOs;

public sealed record SellerStoreDto(Guid Id, string Name, string Phone, string Address);
public sealed record SellerProductDto(Guid Id, string Name, decimal Price, int Stock, bool Active, string Category, string ImageUrl, int VariantCount = 1);
public sealed record SellerItemDto(Guid ProductId, string Name, int Quantity, decimal Price, decimal Total,
    Guid? Id = null, Guid? VariantId = null, string? Sku = null, string? VariantName = null, string? ImageUrl = null, decimal Discount = 0);
public sealed record SellerOrderVoucherDto(string Code, string Name, string DiscountType, decimal Discount);
public sealed record SellerOrderDto(Guid Id, string Code, string Status, string Customer, string Phone, string Address, DateTimeOffset CreatedAt,
    decimal Subtotal, decimal Discount, decimal ShippingFee, decimal ShippingDiscount, decimal Total, IReadOnlyList<SellerItemDto> Items,
    string? PaymentStatus = null, string? PaymentMethod = null, string? Note = null, DateTimeOffset? PaidAt = null,
    DateTimeOffset? ConfirmedAt = null, DateTimeOffset? CompletedAt = null, DateTimeOffset? CancelledAt = null,
    IReadOnlyList<ShipmentDto>? Shipments = null, IReadOnlyList<OrderHistoryDto>? History = null,
    IReadOnlyList<SellerOrderVoucherDto>? Vouchers = null);
public sealed record SellerSummaryDto(decimal Revenue, int TotalOrders, int Pending, int Ready, int Shipping, int LowStock);
