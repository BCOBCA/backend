using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api")]
public sealed class OrdersController(IOrderService orders, IAuthService auth) : ControllerBase
{
    private Task<Guid> UserId(CancellationToken ct) => auth.GetUserIdAsync(Guid.Parse(User.FindFirst("sub")!.Value), ct);
    [HttpGet("cart")]
    public async Task<IActionResult> Cart(CancellationToken ct) => Ok(ApiResponse<CartDto>.Ok(await orders.CartAsync(await UserId(ct), ct)));
    [HttpPost("cart/items")]
    public async Task<IActionResult> Add(CartItemRequest input, CancellationToken ct) { await orders.AddAsync(await UserId(ct), input, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [HttpPut("cart/items/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCartRequest input, CancellationToken ct) { await orders.UpdateAsync(await UserId(ct), id, input, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [HttpDelete("cart/items/{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct) { await orders.RemoveAsync(await UserId(ct), id, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [HttpPost("orders")]
    public async Task<IActionResult> Checkout(CheckoutRequest input, CancellationToken ct)
    {
        var source = Request.Headers["X-Client-Platform"] == "native" ? input.AffiliateVisitToken : Request.Cookies["bcobca_affiliate"];
        var request = Request.Headers["X-Client-Platform"] == "native" ? input : input with { AffiliateVisitToken = null };
        return StatusCode(201, ApiResponse<OrderDto>.Ok(await orders.CheckoutAsync(await UserId(ct), Request.Headers["Idempotency-Key"].ToString(), request, ct, source ?? "")));
    }
    [HttpPost("orders/preview")]
    public async Task<IActionResult> Preview(CheckoutPreviewRequest input, CancellationToken ct) => Ok(ApiResponse<CheckoutPreviewDto>.Ok(await orders.PreviewAsync(await UserId(ct), input, ct)));
    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<OrderSummaryDto>>.Ok(await orders.OrdersAsync(await UserId(ct), query, ct)));
    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> Order(Guid id, CancellationToken ct) => Ok(ApiResponse<OrderDto>.Ok(await orders.OrderAsync(await UserId(ct), id, ct)));
    [HttpGet("orders/requests/{key}")]
    public async Task<IActionResult> FindRequest(string key, CancellationToken ct) => Ok(ApiResponse<OrderDto?>.Ok(await orders.FindRequestAsync(await UserId(ct), key, ct)));
    [HttpPost("orders/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) { await orders.CancelAsync(await UserId(ct), id, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [Authorize(Roles = "Admin,SuperAdmin"), HttpPut("orders/{id:guid}/status")]
    public async Task<IActionResult> Transition(Guid id, OrderStatusRequest input, CancellationToken ct) { await orders.TransitionAsync(Guid.Parse(User.FindFirst("sub")!.Value), id, input, ct); return Ok(ApiResponse<object>.Ok(new { })); }
}
