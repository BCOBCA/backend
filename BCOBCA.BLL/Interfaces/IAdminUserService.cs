using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;
public interface IAdminUserService
{
    Task<AdminUserSummary> SummaryAsync(Guid actor, CancellationToken ct);
    Task<PageResult<AdminUserRow>> ListAsync(Guid actor, AdminUserQuery query, CancellationToken ct);
    Task<AdminUserDetail> DetailAsync(Guid actor, Guid id, AdminUserDetailQuery query, CancellationToken ct);
    Task StatusAsync(Guid actor, Guid id, AdminUserStatusInput input, CancellationToken ct);
}
