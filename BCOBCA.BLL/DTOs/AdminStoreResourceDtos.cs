namespace BCOBCA.BLL.DTOs;

public sealed record AdminStoreResourceStore(Guid Id, string Name);
public sealed record AdminStoreProductDetail(AdminStoreResourceStore Store, AdminStoreProduct Product, string Description,
    IReadOnlyList<string> Images, IReadOnlyList<SellerProductVariantDto> Variants, IReadOnlyList<ProductSpecificationGroupDto> Specifications,
    int? ShippingWeightGrams, int? ShippingLengthCm, int? ShippingWidthCm, int? ShippingHeightCm);
public sealed record AdminStoreOrderLine(Guid Id, Guid ProductId, string Name, string SKU, string VariantName, string ImageUrl,
    int Quantity, decimal UnitPrice, decimal Discount, decimal Total);
public sealed record AdminStoreOrderDetail(AdminStoreResourceStore Store, Guid Id, string Code, string Status, string PaymentStatus, string PaymentMethod,
    string RecipientName, string RecipientPhone, string ShippingAddress, string Note, DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt, DateTimeOffset? CompletedAt, DateTimeOffset? CancelledAt, DateTimeOffset? PaidAt,
    decimal Subtotal, decimal Discount, decimal TotalAmount, int ItemCount, IReadOnlyList<AdminStoreOrderLine> Items,
    IReadOnlyList<ShipmentDto> Shipments, IReadOnlyList<OrderHistoryDto> History);
