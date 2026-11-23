using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BCOBCA.API.Controllers;

[ApiController, Authorize(Roles = "Admin,SuperAdmin"), Route("api/admin/vouchers")]
public sealed class AdminVouchersController(IVoucherManagementService vouchers) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpGet("stores")] public async Task<IActionResult> Stores(CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<VoucherStoreOptionDto>>.Ok(await vouchers.StoresAsync(AccountId, ct)));
    [HttpGet] public async Task<IActionResult> List([FromQuery] VoucherManagementQuery query, CancellationToken ct) => Ok(ApiResponse<VoucherManagementPageDto>.Ok(await vouchers.ListAsync(AccountId, query.StoreId, query, true, ct)));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Detail(Guid id, [FromQuery] int page = 1, CancellationToken ct = default) => Ok(ApiResponse<VoucherManagementDetailDto>.Ok(await vouchers.DetailAsync(AccountId, null, id, page, true, ct)));
    [HttpPost("{id:guid}/disable")] public async Task<IActionResult> Disable(Guid id, VoucherDisableInput input, CancellationToken ct) => Ok(ApiResponse<ManagedVoucherDto>.Ok(await vouchers.DisableAsync(AccountId, null, id, input, true, ct)));
}
