namespace BCOBCA.BLL.DTOs;

public sealed record WithdrawalRequest(decimal Amount, string BankName = "", string AccountNumber = "", string AccountHolder = "", Guid? BankAccountId = null, Guid? ExpectedConfigurationRevision = null);
public sealed record WithdrawalStatusRequest(string Status, string TransactionReference = "");
public sealed record WithdrawalDto(Guid Id, decimal Amount, string BankName, string AccountNumber, string AccountHolder, string Status, string TransactionReference, DateTimeOffset RequestedAt, DateTimeOffset? ProcessedAt, decimal FeeAmount = 0, decimal NetAmount = 0, decimal? BalanceAfter = null);
public sealed record WithdrawalConfigurationDto(decimal FeeAmount, decimal MinimumAmount, decimal MaximumAmount, Guid Revision, DateTimeOffset? UpdatedAt, Guid? UpdatedByAccountId);
public sealed record UpdateWithdrawalConfigurationRequest(decimal? FeeAmount, decimal? MinimumAmount, decimal? MaximumAmount, Guid? ExpectedRevision);
public sealed record AffiliateBankAccountDto(Guid Id, string BankName, string AccountNumber, string AccountHolder);
public sealed record BankDetailsDto(string BankName, string AccountNumber, string AccountHolder);
public sealed record WalletTransactionDto(Guid Id, string Type, decimal Amount, decimal BalanceAfter, string ReferenceType, Guid ReferenceId, DateTimeOffset CreatedAt, string ReferenceCode = "", string Status = "POSTED");
public sealed record CommissionDto(Guid Id, Guid OrderItemId, decimal BaseAmount, decimal CommissionRate, decimal CommissionAmount, string Status, DateTimeOffset CreatedAt, DateTimeOffset? ApprovedAt, Guid? OrderId = null, string OrderCode = "", Guid? ProductId = null, string ProductName = "", string RankName = "", int? Rank = null, decimal PaidAmount = 0);
public sealed record LeaderboardDto(Guid Id, string DisplayName, int Referrals, int Rank, string Tier = "", decimal CommissionRate = 0, string AvatarUrl = "", int TierRank = 0, bool IsMe = false);
public sealed record LeaderboardPageDto(IReadOnlyList<LeaderboardDto> Items, int Page, int PageSize, int TotalItems, string Period = "all", LeaderboardDto? MyEntry = null)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
}
