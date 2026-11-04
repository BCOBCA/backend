using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BCOBCA.API.Controllers;

[ApiController, Route("api/affiliate")]
public sealed class AffiliateLinksController(IAffiliateLinkService links, IAuthService auth, IWebHostEnvironment environment, IConfiguration config) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Task<Guid> UserId(CancellationToken ct) => auth.GetUserIdAsync(AccountId, ct);
    [Authorize, HttpGet("invitation")]
    public async Task<IActionResult> Invitation(CancellationToken ct) => Ok(ApiResponse<AffiliateInvitationDto>.Ok(await links.InvitationAsync(await UserId(ct), ct)));
    [Authorize, HttpPost("links")]
    public async Task<IActionResult> Create(AffiliateLinkRequest input, CancellationToken ct) => Ok(ApiResponse<AffiliateLinkDto>.Ok(await links.CreateAsync(await UserId(ct), input.ProductId, ct)));
    [Authorize, HttpGet("links")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<AffiliateLinkDto>>.Ok(await links.LinksAsync(await UserId(ct), ct)));
    [HttpPost("visits"), EnableRateLimiting("affiliate-tracking")]
    public async Task<IActionResult> Track(AffiliateVisitRequest input, CancellationToken ct)
    {
        var origin = Request.Headers.Origin.ToString();
        if (origin != "" && origin != $"{Request.Scheme}://{Request.Host}" && !(config.GetSection("Cors:Origins").Get<string[]>() ?? []).Contains(origin, StringComparer.Ordinal)) throw new BusinessException("Nguồn yêu cầu không được phép", 403);
        var native = Request.Headers["X-Client-Platform"] == "native";
        var result = await links.TrackAsync(input.Code, native ? input.PreviousToken : Request.Cookies["bcobca_affiliate"], User.Identity?.IsAuthenticated == true ? await UserId(ct) : null, ct);
        if (!native) Response.Cookies.Append("bcobca_affiliate", result.Token, new CookieOptions { HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None, Path = "/api", Expires = result.ExpiresAt, IsEssential = true });
        return Ok(ApiResponse<object>.Ok(new { Token = native ? result.Token : null, result.ExpiresAt }));
    }
    [Authorize(Roles = "Admin,SuperAdmin"), HttpGet("products/{id:guid}/policy")]
    public async Task<IActionResult> Policy(Guid id, CancellationToken ct) => Ok(ApiResponse<ProductAffiliatePolicyRequest>.Ok(await links.ProductPolicyAsync(AccountId, id, ct)));
    [Authorize(Roles = "Admin,SuperAdmin"), HttpPut("products/{id:guid}/policy")]
    public async Task<IActionResult> Policy(Guid id, ProductAffiliatePolicyRequest input, CancellationToken ct) { await links.SetProductPolicyAsync(AccountId, id, input, ct); return Ok(ApiResponse<object>.Ok(new { })); }
}
