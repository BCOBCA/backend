using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api/affiliate/withdrawal-settings")]
public sealed class WithdrawalConfigurationController(IWithdrawalConfigurationService settings) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(ApiResponse<WithdrawalConfigurationDto>.Ok(await settings.GetAsync(ct)));

    [Authorize(Roles = "Admin,SuperAdmin"), HttpGet("/api/admin/settings/affiliate-withdrawal")]
    public async Task<IActionResult> AdminGet(CancellationToken ct) => Ok(ApiResponse<WithdrawalConfigurationDto>.Ok(await settings.GetAsync(ct)));

    [Authorize(Roles = "Admin,SuperAdmin"), HttpPut("/api/admin/settings/affiliate-withdrawal")]
    public async Task<IActionResult> Update(UpdateWithdrawalConfigurationRequest input, CancellationToken ct) => Ok(ApiResponse<WithdrawalConfigurationDto>.Ok(await settings.UpdateAsync(Guid.Parse(User.FindFirst("sub")!.Value), input, ct)));
}
