using BCOBCA.BLL.Common;

namespace BCOBCA.BLL.DTOs;
public sealed record AdminUserQuery(int Page = 1, int PageSize = 8, string? Search = null, string Status = "all", string Verification = "all", string Joined = "all", string Sort = "newest");
public sealed record AdminUserDetailQuery(int OrdersPage = 1, int StoresPage = 1, int ReviewsPage = 1, int HistoryPage = 1, int PageSize = 10);
public sealed record AdminUserSummary(int Total, int Active, int Locked, int Verified);
public sealed record AdminUserRow(Guid Id, string FullName, string Nickname, string AvatarUrl, string Email, string Phone, string Role,
    bool Active, bool EmailVerified, bool PhoneVerified, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? LastLoginAt);
public sealed record AdminUserStats(int Orders, decimal CompletedValue, int Stores, int Reviews);
public sealed record AdminUserOrder(Guid Id, string Code, string Status, string PaymentStatus, decimal TotalAmount, int Quantity, string ProductName, string ImageUrl, DateTimeOffset CreatedAt)
{
    public string RecipientName { get; init; } = "";
    public string RecipientPhone { get; init; } = "";
    public string PaymentMethod { get; init; } = "";
    public int ItemCount { get; init; }
}
public sealed record AdminUserOrderDetail(Guid UserId, string UserName, Guid Id, string Code, string Status, string PaymentStatus, string PaymentMethod,
    string RecipientName, string RecipientPhone, string ShippingAddress, string Note, DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt, DateTimeOffset? CompletedAt, DateTimeOffset? CancelledAt, DateTimeOffset? PaidAt,
    decimal Subtotal, decimal Discount, decimal ShippingFee, decimal ShippingDiscount, decimal TotalAmount, int ItemCount,
    IReadOnlyList<AdminStoreOrderLine> Items, IReadOnlyList<ShipmentDto> Shipments, IReadOnlyList<OrderHistoryDto> History);
public sealed record AdminUserStore(Guid Id, string Name, string Slug, string LogoUrl, bool Active, bool Verified, DateTimeOffset CreatedAt);
public sealed record AdminUserReview(Guid Id, string ProductName, string StoreName, short Rating, string Content, bool Visible, string? ReplyContent, DateTimeOffset CreatedAt);
public sealed record AdminUserHistory(Guid Id, string Action, string Reason, string ActorName, DateTimeOffset CreatedAt);
public sealed record AdminUserDetail(AdminUserRow User, DateOnly? DateOfBirth, string ReferralCode, AdminUserStats Stats,
    PageResult<AdminUserOrder> Orders, PageResult<AdminUserStore> Stores, PageResult<AdminUserReview> Reviews, PageResult<AdminUserHistory> History);
public sealed record AdminUserStatusInput(bool Active, string Reason, DateTimeOffset UpdatedAt);
