using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api/seller")]
public sealed class SellerController(ISellerService seller) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpPost("onboard")]
    public async Task<IActionResult> Onboard(CancellationToken ct)
        => Ok(ApiResponse<SellerStoreDto>.Ok(await seller.OnboardAsync(AccountId, ct)));

    [HttpGet("stores")]
    public async Task<IActionResult> Stores(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<SellerStoreDto>>.Ok(await seller.StoresAsync(AccountId, ct)));

    [HttpGet("stores/{storeId:guid}/summary")]
    public async Task<IActionResult> Summary(Guid storeId, CancellationToken ct)
        => Ok(ApiResponse<SellerSummaryDto>.Ok(await seller.SummaryAsync(AccountId, storeId, ct)));

    [HttpGet("stores/{storeId:guid}/products")]
    public async Task<IActionResult> Products(Guid storeId, [FromQuery] CatalogQuery query, CancellationToken ct)
        => Ok(ApiResponse<PageResult<SellerProductDto>>.Ok(await seller.ProductsAsync(AccountId, storeId, query, ct)));

    [HttpGet("stores/{storeId:guid}/orders")]
    public async Task<IActionResult> Orders(Guid storeId, [FromQuery] CatalogQuery query, CancellationToken ct)
        => Ok(ApiResponse<PageResult<SellerOrderDto>>.Ok(await seller.OrdersAsync(AccountId, storeId, query, ct)));

    [HttpGet("stores/{storeId:guid}/orders/{id:guid}")]
    public async Task<IActionResult> Order(Guid storeId, Guid id, CancellationToken ct)
        => Ok(ApiResponse<SellerOrderDto>.Ok(await seller.OrderAsync(AccountId, storeId, id, ct)));
}
