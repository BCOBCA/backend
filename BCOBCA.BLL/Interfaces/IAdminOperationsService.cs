using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IAdminOperationsService
{
    Task<PageResult<OrderSummaryDto>> OrdersAsync(Guid accountId, CatalogQuery query, CancellationToken ct);
    Task<OrderDto> OrderAsync(Guid accountId, Guid id, CancellationToken ct);
    Task<Guid> ShipmentAsync(Guid accountId, ShipmentInput input, CancellationToken ct);
    Task<PageResult<ServiceRequestDto>> RequestsAsync(Guid accountId, CatalogQuery query, CancellationToken ct);
    Task<PageResult<FeedbackDto>> FeedbacksAsync(Guid accountId, CatalogQuery query, CancellationToken ct);
    Task ReplyAsync(Guid accountId, Guid id, FeedbackReplyRequest input, CancellationToken ct);
}
