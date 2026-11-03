using BCOBCA.API.Extensions;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace BCOBCA.Tests;

[CollectionDefinition("Hosting environment", DisableParallelization = true)]
public sealed class HostingEnvironmentCollection;

[Collection("Hosting environment")]
public sealed class HostingConfigurationTests
{
    [Theory]
    [InlineData("http://+:8080", null, "http://[::]:8080")]
    [InlineData("http://localhost:5000", null, "http://localhost:5000")]
    [InlineData("http://localhost:5000", "10000", "http://0.0.0.0:10000")]
    public async Task ListenerUsesConfiguredInterfaceAndRenderPort(string urls, string? port, string expected)
    {
        using var environment = new HostingEnvironment(urls, port);
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.ConfigureEnvironmentHosting();
        await using var app = builder.Build();
        // Starting the real server confirms Kestrel consumes the restored host configuration.
        await app.StartAsync();
        Assert.Contains(expected, app.Urls);
        await app.StopAsync();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("0")]
    [InlineData("65536")]
    public void InvalidRenderPortFailsWithConfigurationError(string port)
    {
        using var environment = new HostingEnvironment("http://localhost:5000", port);
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        var error = Assert.Throws<InvalidOperationException>(() => builder.ConfigureEnvironmentHosting());
        Assert.Contains("PORT", error.Message);
    }

    private sealed class HostingEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> saved = new();

        public HostingEnvironment(string urls, string? port)
        {
            foreach (var key in new[] { "URLS", "DOTNET_URLS", "ASPNETCORE_URLS", "PORT" })
            {
                saved[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, key switch
                {
                    "ASPNETCORE_URLS" => urls,
                    "PORT" => port,
                    _ => null
                });
            }
        }

        public void Dispose()
        {
            foreach (var (key, value) in saved) Environment.SetEnvironmentVariable(key, value);
        }
    }
}
