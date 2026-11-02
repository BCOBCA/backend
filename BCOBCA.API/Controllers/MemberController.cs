using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api")]
public sealed class MemberController(IMemberService member, IAuthService auth) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Task<Guid> UserId(CancellationToken ct) => auth.GetUserIdAsync(AccountId, ct);

    [HttpGet("membership")]
    public async Task<IActionResult> Membership(CancellationToken ct) => Ok(ApiResponse<MembershipDto>.Ok(await member.MembershipAsync(await UserId(ct), ct)));
    [HttpPost("membership/check-ins")]
    public async Task<IActionResult> CheckIn(CancellationToken ct) => Ok(ApiResponse<CheckInDto>.Ok(await member.CheckInAsync(await UserId(ct), ct)));
    [HttpGet("membership/points")]
    public async Task<IActionResult> Points([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<PointDto>>.Ok(await member.PointsAsync(await UserId(ct), query, ct)));
    [HttpGet("vouchers")]
    public async Task<IActionResult> Vouchers([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<VoucherDto>>.Ok(await member.VouchersAsync(await UserId(ct), query, ct)));
    [HttpPost("vouchers/{id:guid}/claim")]
    public async Task<IActionResult> Claim(Guid id, CancellationToken ct) { await member.ClaimAsync(await UserId(ct), id, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [HttpGet("favorites")]
    public async Task<IActionResult> Favorites([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<CatalogItemDto>>.Ok(await member.FavoritesAsync(await UserId(ct), query, ct)));
    [HttpPut("favorites/{id:guid}")]
    public async Task<IActionResult> Favorite(Guid id, CancellationToken ct) { await member.FavoriteAsync(await UserId(ct), id, true, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [HttpDelete("favorites/{id:guid}")]
    public async Task<IActionResult> Unfavorite(Guid id, CancellationToken ct) { await member.FavoriteAsync(await UserId(ct), id, false, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [HttpGet("addresses")]
    public async Task<IActionResult> Addresses(CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<AddressDto>>.Ok(await member.AddressesAsync(await UserId(ct), ct)));
    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress(AddressRequest input, CancellationToken ct) => StatusCode(201, ApiResponse<AddressDto>.Ok(await member.SaveAddressAsync(await UserId(ct), null, input, ct)));
    [HttpPut("addresses/{id:guid}")]
    public async Task<IActionResult> UpdateAddress(Guid id, AddressRequest input, CancellationToken ct) => Ok(ApiResponse<AddressDto>.Ok(await member.SaveAddressAsync(await UserId(ct), id, input, ct)));
    [HttpDelete("addresses/{id:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid id, CancellationToken ct) { await member.DeleteAddressAsync(await UserId(ct), id, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [HttpGet("affiliate")]
    public async Task<IActionResult> Affiliate(CancellationToken ct) => Ok(ApiResponse<AffiliateDto>.Ok(await member.AffiliateAsync(await UserId(ct), ct)));
    [HttpGet("affiliate/referrals")]
    public async Task<IActionResult> Referrals([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<ReferralDto>>.Ok(await member.ReferralsAsync(await UserId(ct), query, ct)));
    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<NotificationDto>>.Ok(await member.NotificationsAsync(AccountId, query, ct)));
    [HttpPut("notifications/{id:guid}/read")]
    public async Task<IActionResult> ReadNotification(Guid id, CancellationToken ct) { await member.ReadNotificationAsync(AccountId, id, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [HttpPost("feedbacks")]
    public async Task<IActionResult> Feedback(FeedbackRequest input, CancellationToken ct) => StatusCode(201, ApiResponse<object>.Ok(new { id = await member.FeedbackAsync(await UserId(ct), input, ct) }));
    [HttpGet("feedbacks")]
    public async Task<IActionResult> Feedbacks([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<FeedbackDto>>.Ok(await member.FeedbacksAsync(await UserId(ct), query, ct)));
    [HttpGet("services/quotations")]
    public async Task<IActionResult> Quotations([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<QuotationDto>>.Ok(await member.QuotationsAsync(await UserId(ct), query, ct)));
}
