using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IRefundSettlementService
{
    Task<Guid> RequestRefundAsync(Guid userId, Guid orderId, string idempotencyKey, RefundRequest input, CancellationToken ct);
    Task<PageResult<RefundDto>> RefundsAsync(Guid userId, CatalogQuery query, CancellationToken ct);
    Task<PageResult<RefundDto>> AdminRefundsAsync(Guid accountId, CatalogQuery query, CancellationToken ct);
    Task SetRefundStatusAsync(Guid accountId, Guid id, RefundStatusRequest input, CancellationToken ct);
    Task<PageResult<SettlementDto>> SettlementsAsync(Guid accountId, bool allBrands, CatalogQuery query, CancellationToken ct);
    Task PaySettlementAsync(Guid accountId, Guid id, SettlementPaymentRequest input, CancellationToken ct);
    Task<PageResult<SettlementAdjustmentDto>> AdjustmentsAsync(Guid accountId, CatalogQuery query, CancellationToken ct);
}
