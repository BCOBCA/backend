using System.Text.Json;
using System.Text.RegularExpressions;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BCOBCA.BLL.Services;

public sealed class SellerVerificationService(ICommerceStore store, ISellerService seller,
    IVerificationDocumentStorage documents, ILogger<SellerVerificationService> logger) : ISellerVerificationService
{
    private async Task<bool> CheckSubmissionAsync(Guid accountId, Guid storeId, SellerVerificationInput input, CancellationToken ct)
    {
        var profile = await seller.ProfileAsync(accountId, storeId, ct);
        if (await store.Set<StoreVerification>().AnyAsync(v => v.Id == input.SubmissionId && v.StoreId == storeId, ct)) return true;
        if (profile.IsVerified) throw new BusinessException("Cửa hàng đã được xác minh.", 409);
        if (await store.Set<StoreVerification>().AnyAsync(v => v.StoreId == storeId && v.Status == 0, ct))
            throw new BusinessException("Hồ sơ đang được xét duyệt. Vui lòng chờ kết quả.", 409);
        if (profile.UpdatedAt != input.UpdatedAt)
            throw new BusinessException("Thông tin cửa hàng vừa thay đổi. Vui lòng tải lại trước khi gửi.", 409);
        return false;
    }

    public async Task<SellerStoreProfileDto> SubmitAsync(Guid accountId, Guid storeId, SellerVerificationInput input, CancellationToken ct)
    {
        input = input with { LegalName = input.LegalName?.Trim() ?? "", TaxCode = input.TaxCode?.Trim() ?? "",
            RepresentativeName = input.RepresentativeName?.Trim() ?? "" };
        if (input.SubmissionId == Guid.Empty || input.UpdatedAt == default || input.LegalName.Length is < 1 or > 255
            || input.RepresentativeName.Length is < 1 or > 200 || !Regex.IsMatch(input.TaxCode, @"^[0-9]{10}(-?[0-9]{3})?$"))
            throw new BusinessException("Vui lòng nhập tên pháp lý, mã số thuế hợp lệ (10 hoặc 13 số) và người đại diện.", 422);
        if (input.Files is null || input.Files.Count != 3 || !VerificationDocuments.Kinds.All(k => input.Files.Count(f => f.Kind == k) == 1))
            throw new BusinessException("Vui lòng chọn giấy phép kinh doanh và đủ hai mặt CCCD.", 422);
        foreach (var file in input.Files) VerificationDocuments.Validate(file);
        if (await CheckSubmissionAsync(accountId, storeId, input, ct)) return await seller.ProfileAsync(accountId, storeId, ct);

        // Each upload attempt has its own namespace, including concurrent retries of the same submission.
        var attempt = Guid.NewGuid().ToString("N");
        var stored = input.Files.Select(f => new VerificationDocument(f.Kind, f.FileName, f.ContentType, f.Content.LongLength,
            $"bcobca/verifications/{storeId:N}/{attempt}/{f.Kind}{Path.GetExtension(f.FileName).ToLowerInvariant()}")).ToArray();
        var commitStarted = false;
        try
        {
            for (var i = 0; i < stored.Length; i++) await documents.UploadAsync(stored[i].PublicId, input.Files[i], ct);
            await using var tx = await store.BeginTransactionAsync(ct);
            if (await CheckSubmissionAsync(accountId, storeId, input, ct))
            {
                await tx.RollbackAsync(ct);
                await CleanupAsync(stored);
                return await seller.ProfileAsync(accountId, storeId, ct);
            }
            var shop = await store.Set<Store>().SingleAsync(s => s.Id == storeId, ct);
            var now = DateTimeOffset.UtcNow;
            shop.UpdatedAt = now; // Serialize concurrent applications through the existing store row.
            store.Set<StoreVerification>().Add(new StoreVerification {
                Id = input.SubmissionId, StoreId = storeId, VerificationType = 1, LegalName = input.LegalName,
                TaxCode = input.TaxCode, RepresentativeName = input.RepresentativeName,
                Documents = JsonSerializer.Serialize(stored), Status = 0, CreatedAt = now, UpdatedAt = now
            });
            await store.SaveChangesAsync(ct);
            commitStarted = true;
            await tx.CommitAsync(ct);
            return await seller.ProfileAsync(accountId, storeId, ct);
        }
        catch
        {
            // Never delete files if a commit's outcome is uncertain (e.g. connection dropped during COMMIT).
            if (!commitStarted) await CleanupAsync(stored);
            throw;
        }
    }

    private async Task CleanupAsync(IEnumerable<VerificationDocument> files)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        foreach (var file in files)
        {
            try { await documents.DeleteAsync(file.PublicId, timeout.Token); }
            catch (Exception ex) { logger.LogWarning("Verification upload cleanup failed: {ExceptionType}", ex.GetType().Name); }
        }
    }

    public async Task<VerificationDownload> DownloadAsync(Guid accountId, Guid storeId, Guid verificationId, string kind, CancellationToken ct)
    {
        await seller.ProfileAsync(accountId, storeId, ct);
        var application = await store.Set<StoreVerification>().AsNoTracking().SingleOrDefaultAsync(v => v.Id == verificationId && v.StoreId == storeId, ct)
            ?? throw new BusinessException("Không tìm thấy hồ sơ xác minh.", 404);
        var document = VerificationDocuments.Read(application.Documents).SingleOrDefault(d => d.Kind == kind)
            ?? throw new BusinessException("Không tìm thấy giấy tờ.", 404);
        return new(await documents.ReadAsync(document, ct), document.ContentType, document.FileName);
    }
}
