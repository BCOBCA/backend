using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api")]
public sealed class RefundSettlementController(IRefundSettlementService refunds, IAuthService auth) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpPost("orders/{id:guid}/refunds")]
    public async Task<IActionResult> RequestRefund(Guid id, RefundRequest input, CancellationToken ct) => StatusCode(201, ApiResponse<ResourceIdDto>.Ok(new(await refunds.RequestRefundAsync(await auth.GetUserIdAsync(AccountId, ct), id, Request.Headers["Idempotency-Key"].ToString(), input, ct))));
    [HttpGet("refunds")]
    public async Task<IActionResult> Refunds([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<RefundDto>>.Ok(await refunds.RefundsAsync(await auth.GetUserIdAsync(AccountId, ct), query, ct)));
    [Authorize(Roles = "Admin,SuperAdmin"), HttpGet("admin/refunds")]
    public async Task<IActionResult> AdminRefunds([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<RefundDto>>.Ok(await refunds.AdminRefundsAsync(AccountId, query, ct)));
    [Authorize(Roles = "Admin,SuperAdmin"), HttpPut("admin/refunds/{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, RefundStatusRequest input, CancellationToken ct) { await refunds.SetRefundStatusAsync(AccountId, id, input, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [HttpGet("seller/settlements")]
    public async Task<IActionResult> SellerSettlements([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<SettlementDto>>.Ok(await refunds.SettlementsAsync(AccountId, false, query, ct)));
    [Authorize(Roles = "Admin,SuperAdmin"), HttpGet("admin/settlements")]
    public async Task<IActionResult> AllSettlements([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<SettlementDto>>.Ok(await refunds.SettlementsAsync(AccountId, true, query, ct)));
    [Authorize(Roles = "Admin,SuperAdmin"), HttpPost("admin/settlements/{id:guid}/payment")]
    public async Task<IActionResult> Pay(Guid id, SettlementPaymentRequest input, CancellationToken ct) { await refunds.PaySettlementAsync(AccountId, id, input, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [Authorize(Roles = "Admin,SuperAdmin"), HttpGet("admin/settlement-adjustments")]
    public async Task<IActionResult> Adjustments([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<SettlementAdjustmentDto>>.Ok(await refunds.AdjustmentsAsync(AccountId, query, ct)));
}
