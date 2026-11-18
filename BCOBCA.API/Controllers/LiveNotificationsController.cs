using BCOBCA.BLL.Common;
using BCOBCA.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api/notifications"), EnableRateLimiting("live-notifications")]
public sealed class LiveNotificationsController(LiveNotificationService notifications) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpGet("live")]
    public async Task<IActionResult> Live([FromQuery] Guid? after, CancellationToken ct)
        => Ok(ApiResponse<LiveNotificationsDto>.Ok(await notifications.WaitAsync(AccountId, after, ct)));
    [HttpPut("device")]
    public async Task<IActionResult> Register(PushDeviceInput input, CancellationToken ct)
    { await notifications.RegisterAsync(AccountId, Guid.Parse(User.FindFirst("sid")!.Value), input, true, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [HttpDelete("device")]
    public async Task<IActionResult> Revoke(PushDeviceInput input, CancellationToken ct)
    { await notifications.RegisterAsync(AccountId, Guid.Parse(User.FindFirst("sid")!.Value), input, false, ct); return Ok(ApiResponse<object>.Ok(new { })); }
}
