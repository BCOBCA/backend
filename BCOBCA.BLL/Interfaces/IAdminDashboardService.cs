using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IAdminDashboardService
{
    Task<AdminDashboardDto> GetAsync(Guid accountId, DateOnly from, DateOnly to, CancellationToken ct);
}
