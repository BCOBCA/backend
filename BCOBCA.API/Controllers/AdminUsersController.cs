using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;
[ApiController, Authorize(Roles = "Admin,SuperAdmin"), Route("api/admin/users")]
public sealed class AdminUsersController(IAdminUserService users) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpGet("summary")] public async Task<IActionResult> Summary(CancellationToken ct) { Response.Headers.CacheControl = "no-store"; return Ok(ApiResponse<AdminUserSummary>.Ok(await users.SummaryAsync(Actor, ct))); }
    [HttpGet] public async Task<IActionResult> List([FromQuery] AdminUserQuery query, CancellationToken ct) { Response.Headers.CacheControl = "no-store"; return Ok(ApiResponse<PageResult<AdminUserRow>>.Ok(await users.ListAsync(Actor, query, ct))); }
    [HttpGet("{id:guid}")] public async Task<IActionResult> Detail(Guid id, [FromQuery] AdminUserDetailQuery query, CancellationToken ct) { Response.Headers.CacheControl = "no-store"; return Ok(ApiResponse<AdminUserDetail>.Ok(await users.DetailAsync(Actor, id, query, ct))); }
    [HttpPut("{id:guid}/status")] public async Task<IActionResult> Status(Guid id, AdminUserStatusInput input, CancellationToken ct) { await users.StatusAsync(Actor, id, input, ct); return Ok(ApiResponse<object>.Ok(new { })); }
}
