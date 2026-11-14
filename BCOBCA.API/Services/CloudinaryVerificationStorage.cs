using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.BLL.Services;

namespace BCOBCA.API.Services;

// Identity documents must never pass through the public image-upload service.
public sealed class CloudinaryVerificationStorage(HttpClient client, IConfiguration configuration) : IVerificationDocumentStorage
{
    private (string Cloud, string Key, string Secret) Settings()
    {
        var cloud = configuration["Cloudinary:CloudName"] ?? "";
        var key = configuration["Cloudinary:ApiKey"] ?? "";
        var secret = configuration["Cloudinary:ApiSecret"] ?? "";
        if (!Regex.IsMatch(cloud, "^[a-zA-Z0-9_-]{1,100}$") || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
            throw new BusinessException("Dịch vụ lưu giấy tờ chưa được cấu hình.", 503);
        return (cloud, key, secret);
    }

    private static void ValidateId(string publicId)
    {
        if (!Regex.IsMatch(publicId, @"^bcobca/verifications/[a-f0-9]{32}/[a-f0-9]{32}/(license|identityFront|identityBack)\.(pdf|jpg|jpeg|png)$"))
            throw new BusinessException("Thông tin lưu trữ giấy tờ không hợp lệ.", 502);
    }

    private HttpRequestMessage Request(string path, HttpContent content)
    {
        var (cloud, key, secret) = Settings();
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.cloudinary.com/v1_1/{cloud}/raw/{path}") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:{secret}")));
        return request;
    }

    public async Task UploadAsync(string publicId, VerificationFileInput file, CancellationToken ct)
    {
        ValidateId(publicId);
        VerificationDocuments.Validate(file);
        using var form = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(file.Content);
        bytes.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        form.Add(bytes, "file", "document" + Path.GetExtension(file.FileName).ToLowerInvariant());
        form.Add(new StringContent(publicId), "public_id");
        form.Add(new StringContent("false"), "overwrite");
        // In the REST API the delivery type is the endpoint, not the SDK's `type` option.
        using var request = Request("authenticated", form);
        await ExecuteAsync(async () =>
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw StorageError();
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || ReadString(root, "public_id") != publicId
                || ReadString(root, "resource_type") != "raw" || ReadString(root, "type") != "authenticated"
                || !root.TryGetProperty("bytes", out var length) || length.ValueKind != JsonValueKind.Number
                || !length.TryGetInt64(out var size) || size != file.Content.LongLength)
                throw StorageError();
        }, ct);
    }

    public async Task DeleteAsync(string publicId, CancellationToken ct)
    {
        ValidateId(publicId);
        using var request = Request("destroy", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["public_id"] = publicId, ["type"] = "authenticated", ["invalidate"] = "true"
        }));
        await ExecuteAsync(async () =>
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw StorageError();
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (ReadString(json.RootElement, "result") is not ("ok" or "not found")) throw StorageError();
        }, ct);
    }

    public async Task<byte[]> ReadAsync(VerificationDocument document, CancellationToken ct)
    {
        ValidateId(document.PublicId);
        if (document.Size is < 1 or > VerificationDocuments.MaxFileBytes) throw StorageError();
        var (cloud, _, secret) = Settings();
        // The signed delivery URL remains server-side; every client download is authorized by our API.
        var signature = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(document.PublicId + secret)))
            .Replace('+', '-').Replace('/', '_')[..8];
        var uri = $"https://res.cloudinary.com/{cloud}/raw/authenticated/s--{signature}--/{document.PublicId}";
        byte[] result = [];
        await ExecuteAsync(async () =>
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > VerificationDocuments.MaxFileBytes) throw StorageError();
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int count;
            while ((count = await source.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + count > document.Size) throw StorageError();
                await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
            }
            if (buffer.Length != document.Size) throw StorageError();
            result = buffer.ToArray();
            VerificationDocuments.Validate(new(document.Kind, document.FileName, document.ContentType, result));
        }, ct);
        return result;
    }

    private static string? ReadString(JsonElement root, string key) => root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static BusinessException StorageError() => new("Không thể xử lý giấy tờ trên dịch vụ lưu trữ. Vui lòng thử lại.", 502);
    private static async Task ExecuteAsync(Func<Task> action, CancellationToken ct)
    {
        try { await action(); }
        catch (HttpRequestException) { throw StorageError(); }
        catch (JsonException) { throw StorageError(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new BusinessException("Tải giấy tờ quá thời gian. Vui lòng thử lại.", 504); }
    }
}
