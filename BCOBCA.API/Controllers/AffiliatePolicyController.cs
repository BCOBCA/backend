using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, AllowAnonymous, Route("api/affiliate/policy")]
public sealed class AffiliatePolicyController(IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<AffiliatePolicyDto>> Get()
        => Ok(ApiResponse<AffiliatePolicyDto>.Ok(AffiliatePolicyConfiguration.Read(configuration)));
}
