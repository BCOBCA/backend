using BCOBCA.API.Controllers;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace BCOBCA.Tests;

public sealed class GhnWebhookSecurityTests
{
    private static GhnWebhookController Controller(string? secret)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Shipping:Ghn:WebhookSecret"] = secret }).Build();
        return new GhnWebhookController(null!, config) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
    }
    [Fact]
    public async Task MissingWebhookConfigurationFailsClosed()
    {
        var error = await Assert.ThrowsAsync<BusinessException>(() => Controller(null).Callback(new(1, "A", "B", "switch_status", "picked", DateTimeOffset.UtcNow), default));
        Assert.Equal(503, error.StatusCode);
    }
    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    public async Task UnauthorizedCallbacksNeverReachTheShipmentService(string supplied)
    {
        var controller = Controller(new string('x', 40));
        controller.Request.Headers["X-GHN-Webhook-Secret"] = supplied;
        Assert.IsType<UnauthorizedResult>(await controller.Callback(new(1, "A", "B", "switch_status", "picked", DateTimeOffset.UtcNow), default));
    }
}
