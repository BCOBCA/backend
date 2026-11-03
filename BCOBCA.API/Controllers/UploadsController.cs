using BCOBCA.API.Services;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api/admin/uploads")]
public sealed class UploadsController(CloudinaryImageService images, ApplicationDbContext db) : ControllerBase
{
    [HttpPost("images"), Consumes("multipart/form-data"), EnableRateLimiting("image-upload")]
    [RequestSizeLimit(CloudinaryImageService.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = CloudinaryImageService.MaxRequestBytes)]
    public async Task<IActionResult> Image([FromForm] IFormFile file, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var accountId)) return Unauthorized();
        // Check current database roles, so revoked administrators cannot spend the upload quota.
        if (!await db.AccountRoles.AnyAsync(x => x.AccountId == accountId && (x.Role == "Admin" || x.Role == "SuperAdmin"), ct))
            throw new BusinessException("Không có quyền quản trị.", 403);
        return Ok(ApiResponse<ImageUploadDto>.Ok(await images.UploadAsync(file, ct)));
    }
}
