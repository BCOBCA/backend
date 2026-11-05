namespace BCOBCA.BLL.DTOs;

public sealed record AffiliatePolicyDto(
    string ApprovalStatus, string? Version, DateOnly? UpdatedOn, DateOnly? EffectiveOn, DateOnly? ApprovedOn,
    int? AttributionDays, int? ApprovalHoldDays,
    decimal? WithdrawalFee, decimal? MinimumWithdrawal, string? PaymentTime, string? AttributionRule,
    string? ComplaintEmail, string? ComplaintResponseTime, string? OperatorName, string? OperatorAddress);
