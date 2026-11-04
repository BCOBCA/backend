using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api")]
public sealed class LoyaltyConfigurationController(ILoyaltyConfigurationService loyalty) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);

    [AllowAnonymous, HttpGet("membership/levels")]
    public async Task<IActionResult> MembershipLevels(CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<MembershipLevelDto>>.Ok(await loyalty.MembershipLevelsAsync(ct)));

    [Authorize(Roles = "Admin,SuperAdmin"), HttpPut("admin/membership/levels")]
    public async Task<IActionResult> UpdateMembershipLevels(UpdateMembershipLevelsRequest input, CancellationToken ct)
    {
        await loyalty.UpdateMembershipLevelsAsync(AccountId, input, ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpGet("affiliate/ranks")]
    public async Task<IActionResult> AffiliateRanks(CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<AffiliateRankDto>>.Ok(await loyalty.AffiliateRanksAsync(ct)));

    [Authorize(Roles = "Admin,SuperAdmin"), HttpPut("admin/affiliate/ranks")]
    public async Task<IActionResult> UpdateAffiliateRanks(UpdateAffiliateRanksRequest input, CancellationToken ct)
    {
        await loyalty.UpdateAffiliateRanksAsync(AccountId, input, ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
