namespace BCOBCA.BLL.DTOs;

public sealed record RefundRequest(string Reason);
public sealed record RefundStatusRequest(string Status, string ProviderRefundCode = "", bool RestockReturnedItems = false);
public sealed record RefundDto(Guid Id, Guid OrderId, decimal Amount, string Reason, string Status, string ProviderRefundCode, bool RestockReturnedItems, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);
public sealed record SettlementDto(Guid Id, Guid OrderId, Guid BrandId, decimal GrossAmount, decimal FeeAmount, decimal NetAmount, decimal PaidAmount, string Status, string TransactionReference, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt, Guid? StoreId = null);
public sealed record SettlementPaymentRequest(string TransactionReference);
public sealed record SettlementAdjustmentDto(Guid Id, Guid SellerSettlementId, Guid RefundId, decimal Amount, decimal RemainingAmount, string Status, DateTimeOffset CreatedAt);
