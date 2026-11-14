namespace BCOBCA.BLL.DTOs;

public sealed record SellerStoreDto(Guid Id, string Name, string Phone, string Address);
public sealed record SellerProductDto(Guid Id, string Name, decimal Price, int Stock, bool Active, string Category, string ImageUrl, int VariantCount = 1);
public sealed record SellerItemDto(Guid ProductId, string Name, int Quantity, decimal Price, decimal Total);
public sealed record SellerOrderDto(Guid Id, string Code, string Status, string Customer, string Phone, string Address, DateTimeOffset CreatedAt,
    decimal Subtotal, decimal Discount, decimal ShippingFee, decimal ShippingDiscount, decimal Total, IReadOnlyList<SellerItemDto> Items);
public sealed record SellerSummaryDto(decimal Revenue, int TotalOrders, int Pending, int Ready, int Shipping, int LowStock);
