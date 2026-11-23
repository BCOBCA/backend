using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api/seller/stores/{storeId:guid}/vouchers")]
public sealed class SellerVouchersController(IVoucherManagementService vouchers) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpGet] public async Task<IActionResult> List(Guid storeId, [FromQuery] VoucherManagementQuery query, CancellationToken ct) => Ok(ApiResponse<VoucherManagementPageDto>.Ok(await vouchers.ListAsync(AccountId, storeId, query, false, ct)));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Detail(Guid storeId, Guid id, [FromQuery] int page = 1, CancellationToken ct = default) => Ok(ApiResponse<VoucherManagementDetailDto>.Ok(await vouchers.DetailAsync(AccountId, storeId, id, page, false, ct)));
    [HttpPost] public async Task<IActionResult> Create(Guid storeId, SellerVoucherInput input, CancellationToken ct) => StatusCode(201, ApiResponse<ManagedVoucherDto>.Ok(await vouchers.SaveAsync(AccountId, storeId, null, input, ct)));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid storeId, Guid id, SellerVoucherInput input, CancellationToken ct) => Ok(ApiResponse<ManagedVoucherDto>.Ok(await vouchers.SaveAsync(AccountId, storeId, id, input, ct)));
    [HttpPost("{id:guid}/duplicate")] public async Task<IActionResult> Duplicate(Guid storeId, Guid id, SellerVoucherInput input, CancellationToken ct) => StatusCode(201, ApiResponse<ManagedVoucherDto>.Ok(await vouchers.DuplicateAsync(AccountId, storeId, id, input, ct)));
    [HttpPost("{id:guid}/disable")] public async Task<IActionResult> Disable(Guid storeId, Guid id, VoucherDisableInput input, CancellationToken ct) => Ok(ApiResponse<ManagedVoucherDto>.Ok(await vouchers.DisableAsync(AccountId, storeId, id, input, false, ct)));
}
