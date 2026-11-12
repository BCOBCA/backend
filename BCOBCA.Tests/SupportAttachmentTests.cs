using System.Net;
using System.Text;
using System.Text.Json;
using BCOBCA.API.Services;
using BCOBCA.BLL.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SupportAttachmentTests
{
    [Theory]
    [InlineData("proof.pdf", "application/pdf", "%PDF-1.7\nproof")]
    [InlineData("notes.txt", "text/plain", "Nội dung hỗ trợ")]
    public async Task DocumentsUseRawUploadWithGeneratedExtension(string name, string mime, string content)
    {
        using var client = new HttpClient(new Handler(async request =>
        {
            Assert.EndsWith("/raw/upload", request.RequestUri!.AbsoluteUri);
            var parts = ((MultipartFormDataContent)request.Content!).ToDictionary(p => p.Headers.ContentDisposition!.Name!.Trim('"'));
            var id = await parts["public_id"].ReadAsStringAsync();
            Assert.EndsWith(Path.GetExtension(name), id);
            Assert.DoesNotContain(name, id);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { public_id = id, resource_type = "raw", secure_url = $"https://res.cloudinary.com/test/raw/upload/v1/{id}" })) };
        }));
        var result = await Service(client).UploadAttachmentAsync(File(name, mime, Encoding.UTF8.GetBytes(content)), default);
        Assert.Contains("/raw/upload/", result.Url);
    }

    [Theory]
    [InlineData("proof.pdf", "application/pdf", "not a PDF")]
    [InlineData("proof.exe", "application/octet-stream", "MZ")]
    [InlineData("proof.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "PK invalid zip")]
    [InlineData("proof.svg", "image/svg+xml", "<svg/>")]
    [InlineData("proof.txt", "text/html", "<script/>")]
    [InlineData("proof.txt", "text/plain", "")]
    public async Task InvalidFilesNeverReachProvider(string name, string mime, string content)
    {
        using var client = new HttpClient(new Handler(_ => throw new InvalidOperationException("No upload expected")));
        await Assert.ThrowsAsync<BusinessException>(() => Service(client).UploadAttachmentAsync(File(name, mime, Encoding.UTF8.GetBytes(content)), default));
    }

    private static CloudinaryImageService Service(HttpClient client) => new(client, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Cloudinary:CloudName"] = "test", ["Cloudinary:ApiKey"] = "key", ["Cloudinary:ApiSecret"] = "secret" }).Build());
    private static IFormFile File(string name, string mime, byte[] bytes) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = mime };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }
}
