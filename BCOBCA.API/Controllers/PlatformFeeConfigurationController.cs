using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize(Roles = "Admin,SuperAdmin"), Route("api/admin/settings/platform-fee")]
public sealed class PlatformFeeConfigurationController(IPlatformFeeConfigurationService settings) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(ApiResponse<PlatformFeeConfigurationDto>.Ok(await settings.GetAsync(AccountId, ct)));

    [HttpPut]
    public async Task<IActionResult> Update(UpdatePlatformFeeRequest input, CancellationToken ct)
        => Ok(ApiResponse<PlatformFeeConfigurationDto>.Ok(await settings.UpdateAsync(AccountId, input, ct)));
}
