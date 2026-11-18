using System.Security.Cryptography;
using System.Text;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BCOBCA.API.Controllers;

[ApiController, AllowAnonymous, Route("api/shipping/ghn/callback"), EnableRateLimiting("ghn-webhook")]
public sealed class GhnWebhookController(GhnFulfillmentService shipping, IConfiguration config) : ControllerBase
{
    [HttpPost, RequestSizeLimit(32768)]
    public async Task<IActionResult> Callback(GhnCallback input, CancellationToken ct)
    {
        var expected = config["Shipping:Ghn:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(expected) || expected.Length < 32) throw new BusinessException("Webhook GHN chưa được cấu hình", 503);
        var supplied = Request.Headers["X-GHN-Webhook-Secret"].ToString();
        if (supplied.Length > 256 || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
            return Unauthorized();
        await shipping.CallbackAsync(input, ct);
        return Ok(new { code = 200 });
    }
}
