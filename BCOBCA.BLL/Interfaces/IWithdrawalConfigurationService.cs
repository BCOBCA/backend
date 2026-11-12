using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IWithdrawalConfigurationService
{
    Task<WithdrawalConfigurationDto> GetAsync(CancellationToken ct);
    Task<WithdrawalConfigurationDto> UpdateAsync(Guid accountId, UpdateWithdrawalConfigurationRequest input, CancellationToken ct);
}
