using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BCOBCA.API.Services;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BCOBCA.Tests;

public sealed class CloudinaryVerificationStorageTests
{
    private const string PublicId = "bcobca/verifications/11111111111111111111111111111111/22222222222222222222222222222222/license.pdf";
    private static readonly VerificationFileInput Document = new("license", "private-person-name.pdf", "application/pdf", "%PDF-1.7\nsynthetic\n%%EOF"u8.ToArray());
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["Cloudinary:CloudName"] = "test-cloud", ["Cloudinary:ApiKey"] = "test-key", ["Cloudinary:ApiSecret"] = "test-secret"
    }).Build();
    private static HttpResponseMessage Json(object data) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json") };

    [Fact]
    public async Task UploadUsesUploadActionAndAuthenticatedDeliveryTypeWithoutPersonalFilename()
    {
        using var client = new HttpClient(new Handler(async request =>
        {
            // The upload action and delivery type are independent, as in Cloudinary's official SDK.
            Assert.Equal("https://api.cloudinary.com/v1_1/test-cloud/raw/upload", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
            var parts = Assert.IsType<MultipartFormDataContent>(request.Content).ToDictionary(p => p.Headers.ContentDisposition!.Name!.Trim('"'));
            Assert.Equal(PublicId, await parts["public_id"].ReadAsStringAsync());
            Assert.Equal("authenticated", await parts["type"].ReadAsStringAsync());
            Assert.Equal("false", await parts["overwrite"].ReadAsStringAsync());
            Assert.Equal(Document.Content, await parts["file"].ReadAsByteArrayAsync());
            Assert.DoesNotContain("private-person-name", parts["file"].Headers.ContentDisposition!.ToString());
            return Json(new { public_id = PublicId, resource_type = "raw", type = "authenticated", bytes = Document.Content.Length });
        }));
        await new CloudinaryVerificationStorage(client, Config()).UploadAsync(PublicId, Document, default);
    }

    [Theory]
    [InlineData("upload", "raw", "valid")]
    [InlineData("authenticated", "image", "valid")]
    [InlineData("authenticated", "raw", "wrong-id")]
    [InlineData("authenticated", "raw", "wrong-size")]
    [InlineData("authenticated", "raw", "string-size")]
    public async Task RejectsPublicOrMalformedUploadResponses(string deliveryType, string resourceType, string mode)
    {
        using var client = new HttpClient(new Handler(_ => Task.FromResult(Json(new Dictionary<string, object> {
            ["public_id"] = mode == "wrong-id" ? "other" : PublicId,
            ["type"] = deliveryType, ["resource_type"] = resourceType,
            ["bytes"] = mode == "string-size" ? "invalid" : mode == "wrong-size" ? 0 : Document.Content.Length
        }))));
        var error = await Assert.ThrowsAsync<BusinessException>(() => new CloudinaryVerificationStorage(client, Config()).UploadAsync(PublicId, Document, default));
        Assert.Equal(502, error.StatusCode);
    }

    [Fact]
    public async Task DownloadSignsServerSideAndBoundsReturnedContent()
    {
        var signature = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(PublicId + "test-secret"))).Replace('+', '-').Replace('/', '_')[..8];
        using var client = new HttpClient(new Handler(request =>
        {
            Assert.Equal($"https://res.cloudinary.com/test-cloud/raw/authenticated/s--{signature}--/{PublicId}", request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization); // Never send API credentials to the delivery host.
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Document.Content) });
        }));
        var storage = new CloudinaryVerificationStorage(client, Config());
        var metadata = new VerificationDocument("license", Document.FileName, Document.ContentType, Document.Content.Length, PublicId);
        Assert.Equal(Document.Content, await storage.ReadAsync(metadata, default));
        await Assert.ThrowsAsync<BusinessException>(() => storage.ReadAsync(metadata with { Size = 2 }, default));
        await Assert.ThrowsAsync<BusinessException>(() => storage.ReadAsync(metadata with { PublicId = "../../other" }, default));
    }

    [Fact]
    public async Task CleanupTargetsOnlyAuthenticatedAssetsAndAcceptsAlreadyAbsentFile()
    {
        using var client = new HttpClient(new Handler(async request =>
        {
            Assert.EndsWith("/raw/destroy", request.RequestUri!.AbsoluteUri);
            var body = await request.Content!.ReadAsStringAsync();
            Assert.Contains("type=authenticated", body); Assert.Contains("invalidate=true", body);
            Assert.Contains(Uri.EscapeDataString(PublicId), body);
            return Json(new { result = "not found" });
        }));
        await new CloudinaryVerificationStorage(client, Config()).DeleteAsync(PublicId, default);
    }

    [Fact]
    public async Task MissingConfigurationAndNetworkFailuresDoNotLeakSecrets()
    {
        using var client = new HttpClient(new Handler(_ => throw new HttpRequestException("test-secret")));
        var missing = await Assert.ThrowsAsync<BusinessException>(() => new CloudinaryVerificationStorage(client, new ConfigurationBuilder().Build()).UploadAsync(PublicId, Document, default));
        Assert.Equal(503, missing.StatusCode);
        var network = await Assert.ThrowsAsync<BusinessException>(() => new CloudinaryVerificationStorage(client, Config()).UploadAsync(PublicId, Document, default));
        Assert.Equal(502, network.StatusCode); Assert.DoesNotContain("test-secret", network.Message);
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }
}
