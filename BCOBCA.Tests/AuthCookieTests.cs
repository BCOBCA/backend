using System.Text.Json;
using BCOBCA.API.Controllers;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BCOBCA.Tests;

public sealed class AuthCookieTests
{
    [Fact]
    public async Task HostedLoginSetsPersistentCrossSiteCookieWithoutExposingRefreshTokenToJavaScript()
    {
        var (controller, _) = Create();
        var response = Assert.IsType<OkObjectResult>(await controller.Login(new("user", "password"), default));
        var cookie = Cookie(controller);
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=none", cookie);
        Assert.Contains("partitioned", cookie);
        Assert.Contains("path=/api/auth", cookie);
        Assert.Contains("max-age=2592000", cookie);
        var body = JsonSerializer.Serialize(response.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"refreshToken\":null", body);
        Assert.DoesNotContain("issued-refresh", body);
    }

    [Fact]
    public async Task PlainHttpDevelopmentUsesAUsableSameSiteCookie()
    {
        var (controller, _) = Create(development: true);
        controller.Request.Scheme = "http";
        await controller.Login(new("user", "password"), default);
        var cookie = Cookie(controller);
        Assert.Contains("samesite=lax", cookie);
        Assert.DoesNotContain("; secure", cookie);
        Assert.DoesNotContain("partitioned", cookie);
        Assert.Contains("httponly", cookie);
    }

    [Fact]
    public async Task RefreshReadsTheHttpOnlyCookieRatherThanBrowserSuppliedBodyCredentials()
    {
        var (controller, service) = Create();
        controller.Request.Headers.Cookie = "bcobca_refresh=browser-cookie";
        await controller.Refresh(new("body-must-be-ignored"), default);
        Assert.Equal("browser-cookie", service.LastRefresh);
        Assert.Contains("samesite=none", Cookie(controller));
    }

    [Fact]
    public async Task ReloadRecoversTheUserAndRotatesTheCookie()
    {
        var (controller, service) = Create();
        controller.Request.Headers.Cookie = "bcobca_refresh=browser-cookie";
        var response = Assert.IsType<OkObjectResult>(await controller.Restore(new(null), default));
        Assert.Equal("browser-cookie", service.LastRefresh);
        Assert.Contains("partitioned", Cookie(controller));
        var body = JsonSerializer.Serialize(response.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("access-token", body);
        Assert.Contains("\"refreshToken\":null", body);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LogoutAndExpiredSessionDeleteTheSamePartitionedCookie(bool rejected)
    {
        var (controller, service) = Create();
        controller.Request.Headers.Cookie = "bcobca_refresh=browser-cookie";
        if (rejected) { service.RejectRefresh = true; await controller.Restore(new(null), default); }
        else await controller.Logout(new(null), default);
        var cookie = Cookie(controller);
        Assert.Contains("expires=thu, 01 jan 1970", cookie);
        Assert.Contains("samesite=none", cookie);
        Assert.Contains("partitioned", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("path=/api/auth", cookie);
    }

    [Theory]
    [InlineData("login")]
    [InlineData("refresh")]
    [InlineData("session")]
    [InlineData("logout")]
    public async Task CrossSiteSessionEndpointsRejectUnapprovedOriginsBeforeCallingAuth(string action)
    {
        var (controller, service) = Create();
        controller.Request.Headers.Origin = "https://unapproved.example.test";
        var error = await Assert.ThrowsAsync<BusinessException>(() => action switch
        {
            "login" => controller.Login(new("user", "password"), default),
            "refresh" => controller.Refresh(new(null), default),
            "session" => controller.Restore(new(null), default),
            _ => controller.Logout(new(null), default),
        });
        Assert.Equal(403, error.StatusCode);
        Assert.Equal(0, service.Calls);
        Assert.Equal(0, controller.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public async Task NativeRefreshKeepsSecureStoreBodyTransportAndDoesNotSetBrowserCookie()
    {
        var (controller, service) = Create();
        controller.Request.Headers["X-Client-Platform"] = "native";
        controller.Request.Headers.Cookie = "bcobca_refresh=browser-cookie";
        var response = Assert.IsType<OkObjectResult>(await controller.Refresh(new("native-token"), default));
        Assert.Equal("native-token", service.LastRefresh);
        Assert.Equal(0, controller.Response.Headers.SetCookie.Count);
        Assert.Contains("issued-refresh", JsonSerializer.Serialize(response.Value));
    }

    private static string Cookie(AuthController controller) => controller.Response.Headers.SetCookie.ToString().ToLowerInvariant();

    private static (AuthController, StubAuth) Create(bool development = false)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:RefreshTokenDays"] = "30", ["Cors:Origins:0"] = "http://localhost:8082",
        }).Build();
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https"; context.Request.Host = new HostString("api.example.test");
        context.Request.Headers.Origin = "http://localhost:8082";
        var service = new StubAuth();
        var environment = new StubEnvironment { EnvironmentName = development ? Environments.Development : Environments.Production };
        return (new AuthController(service, environment, config) { ControllerContext = new ControllerContext { HttpContext = context } }, service);
    }

    private sealed class StubEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class StubAuth : IAuthService
    {
        public int Calls { get; private set; }
        public string? LastRefresh { get; private set; }
        public bool RejectRefresh { get; set; }
        private static AuthResult Result => new("access-token", "issued-refresh", DateTimeOffset.UtcNow.AddMinutes(15),
            new ProfileDto(Guid.NewGuid(), Guid.NewGuid(), "test@example.test", "", "Test", "", "CODE", ["Customer"]));
        public Task<AuthResult> LoginAsync(LoginRequest input, CancellationToken ct) { Calls++; return Task.FromResult(Result); }
        public Task<AuthResult> RefreshAsync(string token, CancellationToken ct)
        {
            Calls++; LastRefresh = token;
            if (RejectRefresh) throw new BusinessException("Expired", 401);
            return Task.FromResult(Result);
        }
        public Task LogoutAsync(string token, CancellationToken ct) { Calls++; return Task.CompletedTask; }
        public Task<RegistrationResult> RegisterAsync(RegisterRequest input, CancellationToken ct) => throw new NotSupportedException();
        public Task<ProfileDto> GetProfileAsync(Guid accountId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ProfileDto> UpdateProfileAsync(Guid accountId, UpdateProfileRequest input, CancellationToken ct) => throw new NotSupportedException();
        public Task<Guid> GetUserIdAsync(Guid accountId, CancellationToken ct) => throw new NotSupportedException();
    }
}
