using System.ComponentModel.DataAnnotations;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api/seller/stores/{storeId:guid}/verifications")]
public sealed class SellerVerificationsController(ISellerVerificationService verifications, ISellerService seller) : ControllerBase
{
    public const long MaxRequestBytes = 3L * VerificationDocuments.MaxFileBytes + 128 * 1024;
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpPost, Consumes("multipart/form-data"), EnableRateLimiting("image-upload")]
    [RequestSizeLimit(MaxRequestBytes), RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Submit(Guid storeId, [FromForm] VerificationForm input, CancellationToken ct)
    {
        await seller.ProfileAsync(AccountId, storeId, ct); // Verify ownership before reading/uploading document contents.
        var files = new[] { ("license", input.License), ("identityFront", input.IdentityFront), ("identityBack", input.IdentityBack) };
        var uploads = new List<VerificationFileInput>();
        foreach (var (kind, file) in files)
        {
            if (file.Length <= 0 || file.Length > VerificationDocuments.MaxFileBytes)
                throw new BusinessException("Mỗi giấy tờ phải có dữ liệu và không vượt quá 10 MB.", 413);
            await using var buffer = new MemoryStream((int)file.Length);
            await file.CopyToAsync(buffer, ct);
            if (buffer.Length != file.Length) throw new BusinessException("Dữ liệu giấy tờ không hợp lệ.");
            uploads.Add(new(kind, file.FileName, file.ContentType, buffer.ToArray()));
        }
        return Ok(ApiResponse<SellerStoreProfileDto>.Ok(await verifications.SubmitAsync(AccountId, storeId,
            new(input.SubmissionId, input.LegalName, input.TaxCode, input.RepresentativeName, input.UpdatedAt, uploads), ct)));
    }

    [HttpGet("{verificationId:guid}/documents/{kind}")]
    public async Task<IActionResult> Document(Guid storeId, Guid verificationId, string kind, CancellationToken ct)
    {
        var file = await verifications.DownloadAsync(AccountId, storeId, verificationId, kind, ct);
        Response.Headers.CacheControl = "no-store";
        return File(file.Content, file.ContentType, file.FileName);
    }

    public sealed class VerificationForm
    {
        [Required] public Guid SubmissionId { get; set; }
        [Required, StringLength(255)] public string LegalName { get; set; } = "";
        [Required, StringLength(30)] public string TaxCode { get; set; } = "";
        [Required, StringLength(200)] public string RepresentativeName { get; set; } = "";
        [Required] public DateTimeOffset UpdatedAt { get; set; }
        [Required] public IFormFile License { get; set; } = null!;
        [Required] public IFormFile IdentityFront { get; set; } = null!;
        [Required] public IFormFile IdentityBack { get; set; } = null!;
    }
}
