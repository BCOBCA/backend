using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IAffiliateBankAccountService
{
    Task<IReadOnlyList<AffiliateBankAccountDto>> ListAsync(Guid userId, CancellationToken ct);
    Task<AffiliateBankAccountDto> AddAsync(Guid userId, BankDetailsDto input, CancellationToken ct);
}
