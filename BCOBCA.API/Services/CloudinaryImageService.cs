using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.API.Services;

public sealed class CloudinaryImageService(HttpClient client, IConfiguration configuration)
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public const long MaxRequestBytes = MaxImageBytes + 64 * 1024;

    public async Task<ImageUploadDto> UploadAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0) throw new BusinessException("Vui lòng chọn ảnh.");
        if (file.Length > MaxImageBytes) throw new BusinessException("Ảnh không được lớn hơn 5 MB.", 413);

        await using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        if (buffer.Length == 0 || buffer.Length != file.Length || buffer.Length > MaxImageBytes)
            throw new BusinessException("Dữ liệu ảnh không hợp lệ.");
        var mime = DetectImageType(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        if (mime is null || !string.Equals(mime, file.ContentType, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("Chỉ hỗ trợ ảnh JPEG, PNG hoặc WebP hợp lệ.");

        var cloud = configuration["Cloudinary:CloudName"];
        var key = configuration["Cloudinary:ApiKey"];
        var secret = configuration["Cloudinary:ApiSecret"];
        if (string.IsNullOrWhiteSpace(cloud) || !Regex.IsMatch(cloud, "^[a-zA-Z0-9_-]{1,100}$") || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
            throw new BusinessException("Dịch vụ tải ảnh chưa được cấu hình.", 503);

        var publicId = $"bcobca/uploads/{Guid.NewGuid():N}";
        buffer.Position = 0;
        using var form = new MultipartFormDataContent();
        using var image = new StreamContent(buffer);
        image.Headers.ContentType = new MediaTypeHeaderValue(mime);
        // Never forward user-supplied filenames or expose credentials to the caller.
        form.Add(image, "file", "image");
        form.Add(new StringContent(publicId), "public_id");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.cloudinary.com/v1_1/{cloud}/image/upload") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:{secret}")));
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new BusinessException("Không thể tải ảnh lên dịch vụ lưu trữ. Vui lòng thử lại.", 502);
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = json.RootElement;
            var url = ReadString(root, "secure_url");
            var uploadedId = ReadString(root, "public_id");
            var format = ReadString(root, "format");
            if (uploadedId != publicId || ReadString(root, "resource_type") != "image" || format is not ("jpg" or "jpeg" or "png" or "webp")
                || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Host != "res.cloudinary.com"
                || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || !uri.AbsolutePath.StartsWith($"/{cloud}/image/upload/", StringComparison.Ordinal))
                throw new BusinessException("Dịch vụ lưu trữ trả về thông tin ảnh không hợp lệ.", 502);
            return new ImageUploadDto(url!, uploadedId);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new BusinessException("Dịch vụ tải ảnh phản hồi quá chậm. Vui lòng thử lại.", 504);
        }
        catch (HttpRequestException)
        {
            throw new BusinessException("Không thể kết nối dịch vụ tải ảnh. Vui lòng thử lại.", 502);
        }
        catch (JsonException)
        {
            throw new BusinessException("Dịch vụ lưu trữ trả về thông tin ảnh không hợp lệ.", 502);
        }
    }

    private static string? ReadString(JsonElement root, string name) => root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? DetectImageType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.StartsWith(new byte[] { 255, 216, 255 })) return "image/jpeg";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
}
