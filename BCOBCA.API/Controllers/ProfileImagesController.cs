using BCOBCA.API.Services;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api/auth/profile-images")]
public sealed class ProfileImagesController(CloudinaryImageService images, ApplicationDbContext db) : ControllerBase
{
    [HttpPost, Consumes("multipart/form-data"), EnableRateLimiting("image-upload")]
    [RequestSizeLimit(CloudinaryImageService.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = CloudinaryImageService.MaxRequestBytes)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var id) || !await db.Accounts.AnyAsync(a => a.Id == id && a.Status == 1, ct)) return Unauthorized();
        return Ok(ApiResponse<ImageUploadDto>.Ok(await images.UploadAsync(file, ct)));
    }
}
