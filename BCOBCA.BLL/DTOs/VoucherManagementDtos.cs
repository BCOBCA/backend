namespace BCOBCA.BLL.DTOs;

public sealed record SellerVoucherInput(string Code, string Name, string? Description, string DiscountType, decimal DiscountValue,
    decimal? MaxDiscount, decimal MinOrderAmount, int UsageLimit, int PerUserLimit, DateTimeOffset StartsAt, DateTimeOffset ExpiresAt, IReadOnlyList<Guid>? ProductIds);
public sealed record VoucherDisableInput(string Reason);
public sealed record VoucherManagementQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Status = null, Guid? StoreId = null, DateTimeOffset? From = null, DateTimeOffset? To = null);
public sealed record ManagedVoucherDto(Guid Id, Guid? StoreId, string StoreName, string Code, string Name, string Description, string DiscountType,
    decimal DiscountValue, decimal? MaxDiscount, decimal MinOrderAmount, int? UsageLimit, int PerUserLimit, int UsedCount, int ClaimedCount,
    DateTimeOffset StartsAt, DateTimeOffset ExpiresAt, string Status, IReadOnlyList<Guid> ProductIds, bool CanEdit,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string? DisabledReason);
public sealed record VoucherStatisticsDto(int Active, int Upcoming, int Used);
public sealed record VoucherManagementPageDto(BCOBCA.BLL.Common.PageResult<ManagedVoucherDto> Vouchers, VoucherStatisticsDto Statistics);
public sealed record VoucherUsageHistoryDto(Guid Id, Guid OrderId, decimal DiscountAmount, string Status, DateTimeOffset CreatedAt);
public sealed record VoucherActionDto(string Action, string? Reason, DateTimeOffset CreatedAt);
public sealed record VoucherStoreOptionDto(Guid Id, string Name);
public sealed record VoucherManagementDetailDto(ManagedVoucherDto Voucher, BCOBCA.BLL.Common.PageResult<VoucherUsageHistoryDto> Usages, IReadOnlyList<VoucherActionDto> Actions);
