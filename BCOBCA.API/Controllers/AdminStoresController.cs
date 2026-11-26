using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;
[ApiController, Authorize(Roles = "Admin,SuperAdmin"), Route("api/admin/stores")]
public sealed class AdminStoresController(IAdminStoreService stores) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpGet("summary")] public async Task<IActionResult> Summary(CancellationToken ct) => Ok(ApiResponse<AdminStoreSummary>.Ok(await stores.SummaryAsync(AccountId, ct)));
    [HttpGet] public async Task<IActionResult> List([FromQuery] AdminStoreQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<AdminStoreRow>>.Ok(await stores.ListAsync(AccountId, query, ct)));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Detail(Guid id, [FromQuery] int days = 7, CancellationToken ct = default) => Ok(ApiResponse<AdminStoreDetail>.Ok(await stores.DetailAsync(AccountId, id, days, ct)));
    [HttpPost] public async Task<IActionResult> Create(AdminStoreInput input, CancellationToken ct) => Ok(ApiResponse<ResourceIdDto>.Ok(new(await stores.SaveAsync(AccountId, null, input, ct))));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, AdminStoreInput input, CancellationToken ct) => Ok(ApiResponse<ResourceIdDto>.Ok(new(await stores.SaveAsync(AccountId, id, input, ct))));
    [HttpPut("{id:guid}/status")] public async Task<IActionResult> Status(Guid id, AdminStoreStatusInput input, CancellationToken ct) { await stores.StatusAsync(AccountId, id, input, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [HttpPut("{id:guid}/verification")] public async Task<IActionResult> Review(Guid id, AdminStoreReviewInput input, CancellationToken ct) { await stores.ReviewAsync(AccountId, id, input, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [HttpGet("{id:guid}/verifications/{verificationId:guid}/documents/{kind}")]
    public async Task<IActionResult> Document(Guid id, Guid verificationId, string kind, CancellationToken ct) { Response.Headers.CacheControl = "no-store"; return Ok(ApiResponse<AdminStoreDocument>.Ok(await stores.DocumentAsync(AccountId, id, verificationId, kind, ct))); }
}
