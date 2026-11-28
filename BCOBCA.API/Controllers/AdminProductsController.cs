using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize(Roles = "Admin,SuperAdmin"), Route("api/admin/products")]
public sealed class AdminProductsController(IAdminStoreService stores) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminProductQuery query, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(ApiResponse<AdminProductPage>.Ok(await stores.ProductsAsync(Guid.Parse(User.FindFirst("sub")!.Value), query, ct)));
    }
}
