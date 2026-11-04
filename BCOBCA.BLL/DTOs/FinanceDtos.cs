namespace BCOBCA.BLL.DTOs;

public sealed record WithdrawalRequest(decimal Amount, string BankName, string AccountNumber, string AccountHolder);
public sealed record WithdrawalStatusRequest(string Status, string TransactionReference = "");
public sealed record WithdrawalDto(Guid Id, decimal Amount, string BankName, string AccountNumber, string AccountHolder, string Status, string TransactionReference, DateTimeOffset RequestedAt, DateTimeOffset? ProcessedAt);
public sealed record BankDetailsDto(string BankName, string AccountNumber, string AccountHolder);
public sealed record WalletTransactionDto(Guid Id, string Type, decimal Amount, decimal BalanceAfter, string ReferenceType, Guid ReferenceId, DateTimeOffset CreatedAt);
public sealed record CommissionDto(Guid Id, Guid OrderItemId, decimal BaseAmount, decimal CommissionRate, decimal CommissionAmount, string Status, DateTimeOffset CreatedAt, DateTimeOffset? ApprovedAt);
public sealed record LeaderboardDto(Guid Id, string DisplayName, int Referrals, int Rank, string Tier = "", decimal CommissionRate = 0);
