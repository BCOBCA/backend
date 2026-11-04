using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IMemberService
{
    Task<MembershipDto> MembershipAsync(Guid userId, CancellationToken ct);
    Task<CheckInDto> CheckInAsync(Guid userId, CancellationToken ct);
    Task<PageResult<PointDto>> PointsAsync(Guid userId, CatalogQuery query, CancellationToken ct);
    Task<PageResult<VoucherDto>> VouchersAsync(Guid? userId, CatalogQuery query, CancellationToken ct);
    Task ClaimAsync(Guid userId, Guid voucherId, CancellationToken ct);
    Task<PageResult<CatalogItemDto>> FavoritesAsync(Guid userId, CatalogQuery query, CancellationToken ct);
    Task FavoriteAsync(Guid userId, Guid productId, bool value, CancellationToken ct);
    Task<IReadOnlyList<AddressDto>> AddressesAsync(Guid userId, CancellationToken ct);
    Task<AddressDto> SaveAddressAsync(Guid userId, Guid? id, AddressRequest input, CancellationToken ct);
    Task DeleteAddressAsync(Guid userId, Guid id, CancellationToken ct);
    Task<AffiliateDto> AffiliateAsync(Guid userId, CancellationToken ct);
    Task<PageResult<ReferralDto>> ReferralsAsync(Guid userId, CatalogQuery query, CancellationToken ct);
    Task<PageResult<NotificationDto>> NotificationsAsync(Guid accountId, CatalogQuery query, CancellationToken ct);
    Task ReadNotificationAsync(Guid accountId, Guid id, CancellationToken ct);
    Task<Guid> FeedbackAsync(Guid userId, FeedbackRequest input, CancellationToken ct);
    Task<PageResult<FeedbackDto>> FeedbacksAsync(Guid userId, CatalogQuery query, CancellationToken ct);
    Task<PageResult<QuotationDto>> QuotationsAsync(Guid userId, CatalogQuery query, CancellationToken ct);
}
