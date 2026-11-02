using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Route("api")]
public sealed class ReviewsController(IReviewService reviews, IAuthService auth) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpGet("products/{id:guid}/reviews")]
    public async Task<IActionResult> List(Guid id, [FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<ReviewDto>>.Ok(await reviews.ReviewsAsync(id, query, ct)));
    [Authorize, HttpGet("order-items/{id:guid}/review")]
    public async Task<IActionResult> Mine(Guid id, CancellationToken ct) => Ok(ApiResponse<ReviewDto?>.Ok(await reviews.MyReviewAsync(await auth.GetUserIdAsync(AccountId, ct), id, ct)));
    [Authorize, HttpPut("order-items/{id:guid}/review")]
    public async Task<IActionResult> Save(Guid id, ReviewInput input, CancellationToken ct) => Ok(ApiResponse<ResourceIdDto>.Ok(new(await reviews.SaveAsync(await auth.GetUserIdAsync(AccountId, ct), id, input, ct))));
    [Authorize(Roles = "Admin,SuperAdmin"), HttpGet("admin/reviews")]
    public async Task<IActionResult> Pending([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<ReviewDto>>.Ok(await reviews.PendingAsync(AccountId, query, ct)));
    [Authorize(Roles = "Admin,SuperAdmin"), HttpPut("admin/reviews/{id:guid}/status")]
    public async Task<IActionResult> Moderate(Guid id, ReviewModerationRequest input, CancellationToken ct) { await reviews.ModerateAsync(AccountId, id, input.Status, input.ExpectedRevision, ct); return Ok(ApiResponse<object>.Ok(new { })); }
}
