using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IFinanceService
{
    // Called within the order completion transaction, after its payment changes are saved.
    Task AccrueOrderAsync(Guid orderId, CancellationToken ct);
    Task<Guid> WithdrawAsync(Guid userId, string idempotencyKey, WithdrawalRequest input, CancellationToken ct);
    Task<PageResult<WithdrawalDto>> WithdrawalsAsync(Guid userId, CatalogQuery query, CancellationToken ct);
    Task<PageResult<WalletTransactionDto>> TransactionsAsync(Guid userId, CatalogQuery query, CancellationToken ct);
    Task<PageResult<CommissionDto>> CommissionsAsync(Guid userId, CatalogQuery query, CancellationToken ct);
    Task<LeaderboardPageDto> LeaderboardAsync(CatalogQuery query, CancellationToken ct, string period = "all", Guid? userId = null);
    Task ApproveCommissionAsync(Guid accountId, Guid id, CancellationToken ct);
    Task SetWithdrawalStatusAsync(Guid accountId, Guid id, WithdrawalStatusRequest input, CancellationToken ct);
    Task<BankDetailsDto> BankDetailsAsync(Guid accountId, Guid id, CancellationToken ct);
}
