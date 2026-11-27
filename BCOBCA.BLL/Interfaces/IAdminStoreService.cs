using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;
public interface IAdminStoreService
{
    Task<AdminStoreSummary> SummaryAsync(Guid accountId, CancellationToken ct);
    Task<PageResult<AdminStoreRow>> ListAsync(Guid accountId, AdminStoreQuery query, CancellationToken ct);
    Task<AdminStoreDetail> DetailAsync(Guid accountId, Guid id, int days, CancellationToken ct, AdminStoreDetailQuery? query = null);
    Task<Guid> SaveAsync(Guid accountId, Guid? id, AdminStoreInput input, CancellationToken ct);
    Task StatusAsync(Guid accountId, Guid id, AdminStoreStatusInput input, CancellationToken ct);
    Task SuspendOwnerAsync(Guid accountId, Guid id, AdminStoreSuspensionInput input, CancellationToken ct);
    Task ReviewAsync(Guid accountId, Guid id, AdminStoreReviewInput input, CancellationToken ct);
    Task<AdminStoreDocument> DocumentAsync(Guid accountId, Guid id, Guid verificationId, string kind, CancellationToken ct);
}
