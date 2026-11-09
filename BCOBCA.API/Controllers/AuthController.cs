using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BCOBCA.API.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(IAuthService auth, IWebHostEnvironment environment, IConfiguration config) : ControllerBase
{
    private const string CookieName = "bcobca_refresh";
    private bool Native => Request.Headers["X-Client-Platform"] == "native";
    private Guid AccountId => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    [HttpPost("register"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest input, CancellationToken ct)
    {
        await auth.RegisterAsync(input, ct);
        return StatusCode(201, ApiResponse<RegistrationConfirmationDto>.Ok(new()));
    }

    [HttpPost("login"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest input, CancellationToken ct)
        => Ok(Result(await auth.LoginAsync(input, ct)));

    [HttpPost("refresh"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh(RefreshRequest input, CancellationToken ct)
    {
        ValidateOrigin();
        return Ok(Result(await auth.RefreshAsync(ReadToken(input), ct)));
    }

    [HttpPost("session"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Restore(RefreshRequest input, CancellationToken ct)
    {
        ValidateOrigin();
        var token = ReadToken(input);
        if (string.IsNullOrWhiteSpace(token)) return Ok(ApiResponse<object?>.Ok(null));
        try { return Ok(Result(await auth.RefreshAsync(token, ct))); }
        catch (BusinessException ex) when (ex.StatusCode == 401)
        {
            Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/api/auth" });
            return Ok(ApiResponse<object?>.Ok(null));
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest input, CancellationToken ct)
    {
        ValidateOrigin();
        await auth.LogoutAsync(ReadToken(input), ct);
        Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/api/auth" });
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct) => Ok(ApiResponse<ProfileDto>.Ok(await auth.GetProfileAsync(AccountId, ct)));

    [Authorize, HttpPut("me")]
    public async Task<IActionResult> Update(UpdateProfileRequest input, CancellationToken ct) => Ok(ApiResponse<ProfileDto>.Ok(await auth.UpdateProfileAsync(AccountId, input, ct)));

    private string ReadToken(RefreshRequest input) => Native ? input.RefreshToken ?? "" : Request.Cookies[CookieName] ?? "";
    private void ValidateOrigin()
    {
        var origin = Request.Headers.Origin.ToString();
        var sameOrigin = $"{Request.Scheme}://{Request.Host}";
        if (origin != "" && origin != sameOrigin && !(config.GetSection("Cors:Origins").Get<string[]>() ?? []).Contains(origin, StringComparer.Ordinal))
            throw new BusinessException("Nguồn yêu cầu không được phép", 403);
    }

    private ApiResponse<object> Result(AuthResult result)
    {
        if (!Native) Response.Cookies.Append(CookieName, result.RefreshToken, new CookieOptions
        {
            HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Strict,
            Path = "/api/auth", MaxAge = TimeSpan.FromDays(config.GetValue<int>("Jwt:RefreshTokenDays")), IsEssential = true
        });
        return ApiResponse<object>.Ok(new { result.AccessToken, RefreshToken = Native ? result.RefreshToken : null, result.ExpiresAt, result.User });
    }
}
