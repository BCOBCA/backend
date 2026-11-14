using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using BCOBCA.API.Controllers;
using BCOBCA.API.Middlewares;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using BCOBCA.DAL.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerVerificationHttpTests
{
    [Fact]
    public async Task MultipartEndpointBindsAllFilesAndEnforcesOwnershipAndPendingStatus()
    {
        await SellerVerificationTests.WithDatabase(async options =>
        {
            var owner = new Account { Email = "http-verification@example.test", Role = "Seller", Status = 1 };
            var other = new Account { Email = "http-other@example.test", Role = "Seller", Status = 1 };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Shop", Slug = "http-verification", StoreType = 1, Status = 1 };
            DateTimeOffset version;
            await using (var db = new ApplicationDbContext(options))
            {
                db.AddRange(owner, other, shop); await db.SaveChangesAsync();
                version = (await new SellerService(new CommerceStore(db)).ProfileAsync(owner.Id, shop.Id, default)).UpdatedAt;
            }
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddScoped(_ => new ApplicationDbContext(options));
            builder.Services.AddScoped<ICommerceStore, CommerceStore>();
            builder.Services.AddScoped<ISellerService, SellerService>();
            builder.Services.AddScoped<ISellerVerificationService, SellerVerificationService>();
            var storage = new Storage(); builder.Services.AddSingleton<IVerificationDocumentStorage>(storage);
            builder.Services.AddControllers().AddApplicationPart(typeof(SellerVerificationsController).Assembly);
            builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("test", _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("image-upload", limit => { limit.PermitLimit = 30; limit.Window = TimeSpan.FromMinutes(1); }));
            await using var app = builder.Build();
            app.UseMiddleware<ExceptionMiddleware>(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter(); app.MapControllers();
            app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            var route = $"/api/seller/stores/{shop.Id}/verifications";
            var id = Guid.NewGuid();
            using (var form = Form(id, version)) Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(route, form)).StatusCode);
            client.DefaultRequestHeaders.Add("X-Test-Account", other.Id.ToString());
            using (var form = Form(id, version)) Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(route, form)).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Account"); client.DefaultRequestHeaders.Add("X-Test-Account", owner.Id.ToString());
            using (var form = Form(id, version, missing: true)) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(route, form)).StatusCode);
            using (var form = Form(id, version, spoof: true))
            {
                using var response = await client.PostAsync(route, form);
                Assert.True(response.StatusCode == HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());
            }
            Assert.Empty(storage.Files);
            using (var form = Form(id, version))
            {
                using var response = await client.PostAsync(route, form); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var verification = json.RootElement.GetProperty("data").GetProperty("verification");
                Assert.Equal(0, verification.GetProperty("status").GetInt32());
                Assert.Equal(id, verification.GetProperty("id").GetGuid());
                Assert.Equal(3, verification.GetProperty("documents").GetArrayLength());
            }
            using (var form = Form(id, version)) Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(route, form)).StatusCode);
            Assert.Equal(3, storage.Files.Count);
            using (var form = Form(Guid.NewGuid(), version)) Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(route, form)).StatusCode);
            using var download = await client.GetAsync($"{route}/{id}/documents/license");
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal("application/pdf", download.Content.Headers.ContentType!.MediaType);
            Assert.Contains("no-store", download.Headers.CacheControl!.ToString());
            Assert.Equal("%PDF-1.7\nsynthetic\n%%EOF", await download.Content.ReadAsStringAsync());
            client.DefaultRequestHeaders.Remove("X-Test-Account"); client.DefaultRequestHeaders.Add("X-Test-Account", other.Id.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{route}/{id}/documents/license")).StatusCode);
            await app.StopAsync();
        });
    }

    private static MultipartFormDataContent Form(Guid id, DateTimeOffset version, bool missing = false, bool spoof = false)
    {
        var form = new MultipartFormDataContent();
        foreach (var (key, value) in new[] { ("submissionId", id.ToString()), ("updatedAt", version.ToString("O")),
            ("legalName", "Công ty An"), ("taxCode", "0123456789"), ("representativeName", "Nguyễn An") }) form.Add(new StringContent(value), key);
        foreach (var kind in VerificationDocuments.Kinds.Where(k => !missing || k != "identityBack"))
        {
            var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(spoof ? "<html>fake</html>" : "%PDF-1.7\nsynthetic\n%%EOF"));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf"); form.Add(file, kind, kind + ".pdf");
        }
        return form;
    }

    private sealed class Storage : IVerificationDocumentStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public Task UploadAsync(string publicId, VerificationFileInput file, CancellationToken ct) { Files[publicId] = file.Content; return Task.CompletedTask; }
        public Task DeleteAsync(string publicId, CancellationToken ct) { Files.Remove(publicId); return Task.CompletedTask; }
        public Task<byte[]> ReadAsync(VerificationDocument document, CancellationToken ct) => Task.FromResult(Files[document.PublicId]);
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-Account"], out var accountId)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim("sub", accountId.ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
