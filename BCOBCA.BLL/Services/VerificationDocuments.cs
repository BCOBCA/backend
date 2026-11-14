using System.Text.Json;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Services;

public static class VerificationDocuments
{
    public const int MaxFileBytes = 10 * 1024 * 1024;
    public static readonly string[] Kinds = ["license", "identityFront", "identityBack"];

    public static void Validate(VerificationFileInput file)
    {
        if (!Kinds.Contains(file.Kind) || string.IsNullOrWhiteSpace(file.FileName) || file.FileName.Length > 255
            || file.FileName.Any(c => char.IsControl(c) || c is '/' or '\\'))
            throw new BusinessException("Thông tin tệp giấy tờ không hợp lệ.");
        if (file.Content.Length == 0) throw new BusinessException("Không thể gửi tệp trống.");
        if (file.Content.Length > MaxFileBytes) throw new BusinessException("Mỗi tệp không được vượt quá 10 MB.", 413);
        var bytes = file.Content.AsSpan();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var valid = (extension, file.ContentType) switch
        {
            (".pdf", "application/pdf") => bytes.StartsWith("%PDF-"u8),
            (".jpg" or ".jpeg", "image/jpeg") => bytes.StartsWith(new byte[] { 255, 216, 255 }),
            (".png", "image/png") => bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            _ => false
        };
        if (!valid) throw new BusinessException("Nội dung tệp phải đúng định dạng JPG, JPEG, PNG hoặc PDF.");
    }

    public static IReadOnlyList<VerificationDocument> Read(string json)
    {
        try { return (JsonSerializer.Deserialize<VerificationDocument[]>(json) ?? []).Where(d => d is not null).ToArray(); }
        catch (JsonException) { return []; } // Older applications may contain a different document shape.
    }

    public static IReadOnlyList<VerificationDocumentDto> Summaries(string json)
        => Read(json).Where(d => Kinds.Contains(d.Kind) && !string.IsNullOrWhiteSpace(d.FileName))
            .Select(d => new VerificationDocumentDto(d.Kind, d.FileName, d.ContentType, d.Size)).ToArray();
}
