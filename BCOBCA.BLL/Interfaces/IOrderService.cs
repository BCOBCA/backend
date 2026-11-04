using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IOrderService
{
    Task<CartDto> CartAsync(Guid userId, CancellationToken ct);
    Task AddAsync(Guid userId, CartItemRequest input, CancellationToken ct);
    Task UpdateAsync(Guid userId, Guid itemId, UpdateCartRequest input, CancellationToken ct);
    Task RemoveAsync(Guid userId, Guid itemId, CancellationToken ct);
    Task<OrderDto> CheckoutAsync(Guid userId, string idempotencyKey, CheckoutRequest input, CancellationToken ct, string? affiliateVisitToken = null);
    Task<CheckoutPreviewDto> PreviewAsync(Guid userId, CheckoutPreviewRequest input, CancellationToken ct);
    Task<PageResult<OrderSummaryDto>> OrdersAsync(Guid userId, CatalogQuery query, CancellationToken ct);
    Task<OrderDto> OrderAsync(Guid userId, Guid id, CancellationToken ct);
    Task<OrderDto?> FindRequestAsync(Guid userId, string key, CancellationToken ct);
    Task CancelAsync(Guid userId, Guid id, CancellationToken ct);
    Task TransitionAsync(Guid accountId, Guid orderId, OrderStatusRequest input, CancellationToken ct);
    Task<int> ExpireOneReservationAsync(CancellationToken ct);
}
