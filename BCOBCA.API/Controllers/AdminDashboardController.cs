using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize(Roles = "Admin,SuperAdmin"), Route("api/admin/dashboard")]
public sealed class AdminDashboardController(IAdminDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var end = to ?? CommerceRules.VietnamDate(DateTimeOffset.UtcNow);
        var start = from ?? (end.DayNumber >= 6 ? end.AddDays(-6) : end);
        return Ok(ApiResponse<AdminDashboardDto>.Ok(await dashboard.GetAsync(Guid.Parse(User.FindFirst("sub")!.Value), start, end, ct)));
    }
}
