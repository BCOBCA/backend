using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api/admin")]
public sealed class AdminOperationsController(IAdminOperationsService operations) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);
    [Authorize(Roles = "Admin,SuperAdmin"), HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<OrderSummaryDto>>.Ok(await operations.OrdersAsync(AccountId, query, ct)));
    [Authorize(Roles = "Admin,SuperAdmin"), HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> Order(Guid id, CancellationToken ct) => Ok(ApiResponse<OrderDto>.Ok(await operations.OrderAsync(AccountId, id, ct)));
    [Authorize(Roles = "Admin,SuperAdmin"), HttpPost("shipments")]
    public async Task<IActionResult> Shipment(ShipmentInput input, CancellationToken ct) => StatusCode(201, ApiResponse<ResourceIdDto>.Ok(new(await operations.ShipmentAsync(AccountId, input, ct))));
    [HttpGet("service-requests")]
    public async Task<IActionResult> Requests([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<ServiceRequestDto>>.Ok(await operations.RequestsAsync(AccountId, query, ct)));
    [Authorize(Roles = "Admin,SuperAdmin"), HttpGet("feedbacks")]
    public async Task<IActionResult> Feedbacks([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<FeedbackDto>>.Ok(await operations.FeedbacksAsync(AccountId, query, ct)));
    [Authorize(Roles = "Admin,SuperAdmin"), HttpPut("feedbacks/{id:guid}/reply")]
    public async Task<IActionResult> Reply(Guid id, FeedbackReplyRequest input, CancellationToken ct) { await operations.ReplyAsync(AccountId, id, input, ct); return Ok(ApiResponse<object>.Ok(new { })); }
}
