using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api/seller/stores/{storeId:guid}/reviews")]
public sealed class SellerReviewsController(IReviewService reviews) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<IActionResult> List(Guid storeId, [FromQuery] SellerReviewQuery query, CancellationToken ct)
        => Ok(ApiResponse<PageResult<SellerReviewDto>>.Ok(await reviews.SellerReviewsAsync(AccountId, storeId, query, ct)));

    [HttpPut("{id:guid}/reply")]
    public async Task<IActionResult> Reply(Guid storeId, Guid id, ReviewReplyInput input, CancellationToken ct)
        => Ok(ApiResponse<ReviewDto>.Ok(await reviews.ReplyAsync(AccountId, storeId, id, input, ct)));
}
