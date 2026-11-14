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

    [HttpGet("stores/{storeId:guid}/profile")]
    public async Task<IActionResult> Profile(Guid storeId, CancellationToken ct)
        => Ok(ApiResponse<SellerStoreProfileDto>.Ok(await seller.ProfileAsync(AccountId, storeId, ct)));

    [HttpPut("stores/{storeId:guid}/profile")]
    public async Task<IActionResult> UpdateProfile(Guid storeId, SellerStoreProfileInput input, CancellationToken ct)
        => Ok(ApiResponse<SellerStoreProfileDto>.Ok(await seller.UpdateProfileAsync(AccountId, storeId, input, ct)));

    [HttpGet("stores/{storeId:guid}/products")]
    public async Task<IActionResult> Products(Guid storeId, [FromQuery] CatalogQuery query, CancellationToken ct)
        => Ok(ApiResponse<PageResult<SellerProductDto>>.Ok(await seller.ProductsAsync(AccountId, storeId, query, ct)));

    [HttpPost("stores/{storeId:guid}/branches")]
    public async Task<IActionResult> AddBranch(Guid storeId, SellerBranchInput input, CancellationToken ct)
        => Ok(ApiResponse<SellerStoreProfileDto>.Ok(await seller.SaveBranchAsync(AccountId, storeId, null, input, ct)));

    [HttpGet("stores/{storeId:guid}/product-options")]
    public async Task<IActionResult> ProductOptions(Guid storeId, CancellationToken ct)
        => Ok(ApiResponse<SellerProductOptionsDto>.Ok(await seller.ProductOptionsAsync(AccountId, storeId, ct)));

    [HttpPost("stores/{storeId:guid}/products"), RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> CreateProduct(Guid storeId, SellerCreateProductInput input, CancellationToken ct)
        => StatusCode(201, ApiResponse<SellerProductDto>.Ok(await seller.CreateProductAsync(AccountId, storeId, input, ct)));

    [HttpPut("stores/{storeId:guid}/branches/{id:guid}")]
    public async Task<IActionResult> UpdateBranch(Guid storeId, Guid id, SellerBranchInput input, CancellationToken ct)
        => Ok(ApiResponse<SellerStoreProfileDto>.Ok(await seller.SaveBranchAsync(AccountId, storeId, id, input, ct)));

    [HttpPut("stores/{storeId:guid}/pickup-branch")]
    public async Task<IActionResult> PickupBranch(Guid storeId, SellerPickupBranchInput input, CancellationToken ct)
        => Ok(ApiResponse<SellerStoreProfileDto>.Ok(await seller.SelectPickupBranchAsync(AccountId, storeId, input, ct)));

    [HttpPut("stores/{storeId:guid}/main-branch")]
    public async Task<IActionResult> MainBranch(Guid storeId, SellerPickupBranchInput input, CancellationToken ct)
        => Ok(ApiResponse<SellerStoreProfileDto>.Ok(await seller.SelectMainBranchAsync(AccountId, storeId, input, ct)));

    [HttpGet("stores/{storeId:guid}/orders")]
    public async Task<IActionResult> Orders(Guid storeId, [FromQuery] CatalogQuery query, CancellationToken ct)
        => Ok(ApiResponse<PageResult<SellerOrderDto>>.Ok(await seller.OrdersAsync(AccountId, storeId, query, ct)));

    [HttpPut("stores/{storeId:guid}/return-branch")]
    public async Task<IActionResult> ReturnBranch(Guid storeId, SellerPickupBranchInput input, CancellationToken ct)
        => Ok(ApiResponse<SellerStoreProfileDto>.Ok(await seller.SelectReturnBranchAsync(AccountId, storeId, input, ct)));

    [HttpGet("stores/{storeId:guid}/orders/{id:guid}")]
    public async Task<IActionResult> Order(Guid storeId, Guid id, CancellationToken ct)
        => Ok(ApiResponse<SellerOrderDto>.Ok(await seller.OrderAsync(AccountId, storeId, id, ct)));
}
