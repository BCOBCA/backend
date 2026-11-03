using System.Net;
using System.Text;
using System.Text.Json;
using BCOBCA.API.Services;
using BCOBCA.BLL.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BCOBCA.Tests;

public sealed class CloudinaryImageServiceTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
    private const int MaxBytes = 5 * 1024 * 1024;

    [Fact]
    public async Task UploadUsesServerCredentialsAndGeneratedNameThenReturnsHttpsUrl()
    {
        string? publicId = null;
        using var handler = new Handler(async (request, _) =>
        {
            Assert.Equal("https://api.cloudinary.com/v1_1/test-cloud/image/upload", request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
            Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("test-key:test-secret")), request.Headers.Authorization?.Parameter);
            var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
            var parts = multipart.ToDictionary(x => x.Headers.ContentDisposition!.Name!.Trim('"'));
            publicId = await parts["public_id"].ReadAsStringAsync();
            Assert.StartsWith("bcobca/uploads/", publicId);
            Assert.DoesNotContain("private-name", publicId);
            var filePart = parts["file"];
            Assert.Equal("image/png", filePart.Headers.ContentType?.MediaType);
            Assert.Equal(Png, await filePart.ReadAsByteArrayAsync());
            Assert.DoesNotContain("private-name", filePart.Headers.ContentDisposition!.ToString());
            Assert.DoesNotContain("api_secret", parts.Keys);
            return Success(publicId);
        });
        using var client = new HttpClient(handler);
        var result = await new CloudinaryImageService(client, Configuration()).UploadAsync(File(Png, name: "../../private-name.png"), default);
        Assert.Equal(publicId, result.PublicId);
        Assert.Equal($"https://res.cloudinary.com/test-cloud/image/upload/v1/{publicId}.png", result.Url);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("oversized")]
    [InlineData("svg")]
    [InlineData("invalid-bytes")]
    [InlineData("mismatched-type")]
    public async Task InvalidImagesAreRejectedBeforeAnyProviderRequest(string scenario)
    {
        var (data, mime) = scenario switch
        {
            "empty" => (Array.Empty<byte>(), "image/png"),
            "oversized" => (new byte[MaxBytes + 1], "image/png"),
            "svg" => (Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'/>"), "image/svg+xml"),
            "invalid-bytes" => (Encoding.UTF8.GetBytes("This is not an image."), "image/png"),
            _ => (Png, "image/jpeg")
        };
        using var handler = new Handler((_, _) => throw new InvalidOperationException("Provider must not be called."));
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<BusinessException>(() => new CloudinaryImageService(client, Configuration()).UploadAsync(File(data, mime), default));
        Assert.InRange(error.StatusCode, 400, 499);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task ImageAtFiveMebibyteBoundaryIsAccepted()
    {
        var data = new byte[MaxBytes];
        Png.CopyTo(data, 0);
        using var handler = SuccessfulHandler();
        using var client = new HttpClient(handler);
        var result = await new CloudinaryImageService(client, Configuration()).UploadAsync(File(data), default);
        Assert.StartsWith("https://res.cloudinary.com/test-cloud/", result.Url);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("Cloudinary:CloudName")]
    [InlineData("Cloudinary:ApiKey")]
    [InlineData("Cloudinary:ApiSecret")]
    public async Task MissingProviderConfigurationReturnsServiceUnavailable(string missingKey)
    {
        using var handler = SuccessfulHandler();
        using var client = new HttpClient(handler);
        var configuration = Configuration();
        configuration[missingKey] = "";
        var error = await Assert.ThrowsAsync<BusinessException>(() => new CloudinaryImageService(client, configuration).UploadAsync(File(Png), default));
        Assert.Equal(503, error.StatusCode);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"secure_url\":\"http://res.cloudinary.com/test-cloud/image/upload/test.png\",\"public_id\":\"test\"}")]
    [InlineData("{\"secure_url\":\"https://untrusted.example/test.png\",\"public_id\":\"test\"}")]
    [InlineData("{\"secure_url\":\"https://res.cloudinary.com/another-cloud/image/upload/test.png\",\"public_id\":\"test\"}")]
    public async Task InvalidProviderResponsesReturnBadGateway(string body)
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }));
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<BusinessException>(() => new CloudinaryImageService(client, Configuration()).UploadAsync(File(Png), default));
        Assert.Equal(502, error.StatusCode);
    }

    [Fact]
    public async Task ProviderErrorIsSanitized()
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("secret detail: test-secret") }));
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<BusinessException>(() => new CloudinaryImageService(client, Configuration()).UploadAsync(File(Png), default));
        Assert.Equal(502, error.StatusCode);
        Assert.DoesNotContain("test-secret", error.Message);
    }

    [Fact]
    public async Task ProviderTimeoutReturnsGatewayTimeout()
    {
        using var handler = new Handler((_, _) => throw new TaskCanceledException("Provider timeout"));
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<BusinessException>(() => new CloudinaryImageService(client, Configuration()).UploadAsync(File(Png), default));
        Assert.Equal(504, error.StatusCode);
    }

    [Fact]
    public async Task TransportFailureReturnsBadGateway()
    {
        using var handler = new Handler((_, _) => throw new HttpRequestException("Connection failed"));
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<BusinessException>(() => new CloudinaryImageService(client, Configuration()).UploadAsync(File(Png), default));
        Assert.Equal(502, error.StatusCode);
    }

    [Fact]
    public async Task CallerCancellationIsPreserved()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new Handler((_, token) =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(token);
        });
        using var client = new HttpClient(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CloudinaryImageService(client, Configuration()).UploadAsync(File(Png), cancellation.Token));
    }

    private static IConfigurationRoot Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Cloudinary:CloudName"] = "test-cloud", ["Cloudinary:ApiKey"] = "test-key", ["Cloudinary:ApiSecret"] = "test-secret"
    }).Build();

    private static IFormFile File(byte[] data, string type = "image/png", string name = "image.png") => new FormFile(new MemoryStream(data), 0, data.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = type };

    private static Handler SuccessfulHandler() => new(async (request, _) =>
    {
        var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
        var publicId = await multipart.Single(x => x.Headers.ContentDisposition?.Name?.Trim('"') == "public_id").ReadAsStringAsync();
        return Success(publicId);
    });

    private static HttpResponseMessage Success(string publicId) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { secure_url = $"https://res.cloudinary.com/test-cloud/image/upload/v1/{publicId}.png", public_id = publicId, resource_type = "image", format = "png" }), Encoding.UTF8, "application/json")
    };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Calls++; return send(request, cancellationToken); }
    }
}
