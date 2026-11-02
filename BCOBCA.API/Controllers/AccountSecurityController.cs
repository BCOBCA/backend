using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BCOBCA.API.Controllers;

[ApiController, Route("api/auth"), EnableRateLimiting("auth")]
public sealed class AccountSecurityController(IAccountSecurityService security) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpPost("forgot-password")]
    public async Task<IActionResult> Forgot(ForgotPasswordRequest input, CancellationToken ct) { await security.ForgotAsync(input, ct); return Accepted(ApiResponse<object>.Ok(new { }, "Nếu tài khoản tồn tại, hướng dẫn đã được gửi qua email")); }
    [HttpPost("reset-password")]
    public async Task<IActionResult> Reset(ResetPasswordRequest input, CancellationToken ct) { await security.ResetAsync(input, ct); return Ok(ApiResponse<object>.Ok(new { }, "Đã đặt lại mật khẩu. Vui lòng đăng nhập lại")); }
    [Authorize, HttpPost("change-password")]
    public async Task<IActionResult> Change(ChangePasswordRequest input, CancellationToken ct) { await security.ChangeAsync(AccountId, input, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [Authorize, HttpPost("verification-email")]
    public async Task<IActionResult> Send(CancellationToken ct) { await security.SendVerificationAsync(AccountId, ct); return Accepted(ApiResponse<object>.Ok(new { })); }
    [HttpPost("verify-email")]
    public async Task<IActionResult> Verify(VerifyEmailRequest input, CancellationToken ct) { await security.VerifyAsync(input, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [Authorize, HttpGet("sessions")]
    public async Task<IActionResult> Sessions(CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<SessionDto>>.Ok(await security.SessionsAsync(AccountId, ct)));
    [Authorize, HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct) { await security.RevokeAsync(AccountId, id, ct); return Ok(ApiResponse<object>.Ok(new { })); }
}
