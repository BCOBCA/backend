using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IPlatformFeeConfigurationService
{
    Task<PlatformFeeConfigurationDto> GetAsync(Guid accountId, CancellationToken ct);
    Task<PlatformFeeConfigurationDto> UpdateAsync(Guid accountId, UpdatePlatformFeeRequest input, CancellationToken ct);
}
