using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IReviewService
{
    Task<PageResult<ReviewDto>> ReviewsAsync(Guid productId, CatalogQuery query, CancellationToken ct);
    Task<ReviewDto?> MyReviewAsync(Guid userId, Guid orderItemId, CancellationToken ct);
    Task<Guid> SaveAsync(Guid userId, Guid orderItemId, ReviewInput input, CancellationToken ct);
    Task<PageResult<ReviewDto>> PendingAsync(Guid accountId, CatalogQuery query, CancellationToken ct);
    Task ModerateAsync(Guid accountId, Guid id, short status, Guid expectedRevision, CancellationToken ct);
}
