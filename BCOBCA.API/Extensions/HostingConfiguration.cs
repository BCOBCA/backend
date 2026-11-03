using System.Globalization;

namespace BCOBCA.API.Extensions;

public static class HostingConfiguration
{
    public static void ConfigureEnvironmentHosting(this WebApplicationBuilder builder)
    {
        builder.Configuration.Sources.Clear();
        // Keep environment-only configuration without losing ASP.NET Core host keys.
        builder.Configuration.AddEnvironmentVariables("ASPNETCORE_")
            .AddEnvironmentVariables("DOTNET_")
            .AddEnvironmentVariables();

        var port = builder.Configuration["PORT"];
        if (port is null) return;
        if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 1 or > 65535)
            throw new InvalidOperationException("PORT must be an integer between 1 and 65535.");

        // Render must reach the server from outside the container, even with local URL defaults.
        builder.WebHost.UseUrls($"http://0.0.0.0:{number}");
    }
}
