using System.Net;
using System.Text.Json;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BCOBCA.Tests;

public sealed class GhnShopRegistrationTests
{
    internal static readonly Guid StoreId = Guid.Parse("a0b71518-d824-4f11-b165-0b595a31cb89");
    internal static readonly GhnPickup Pickup = new("Kho QQ", "0901234567", "12 Nguyễn Huệ", "Thành phố Hồ Chí Minh", "Phường Sài Gòn");
    internal static IConfiguration Config(string? token = "sandbox-token") => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Shipping:Ghn:TestToken"] = token }).Build();
    internal static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body) };
    internal sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => respond(request);
    }

    // State survives a client/transaction failure, just as the remote provider does.
    internal sealed class Provider : HttpMessageHandler
    {
        public string? Name { get; private set; }
        public int Creates { get; private set; }
        public int Calls { get; private set; }
        public bool FailCreate { get; set; }
        public bool LoseResponse { get; set; }
        public bool SecondPage { get; set; }
        public bool WrongRecoveredAddress { get; set; }
        public bool NewAddressField { get; set; }
        public string CreateResponse { get; set; } = """{"code":200,"data":{"shop_id":456}}""";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Assert.Equal("dev-online-gateway.ghn.vn", request.RequestUri!.Host);
            Assert.Equal("sandbox-token", Assert.Single(request.Headers.GetValues("Token")));
            var path = request.RequestUri.AbsolutePath;
            if (path.EndsWith("/shop/all"))
            {
                var registered = Name is null ? Array.Empty<object>() : [new { _id = 456, name = Name, status = 1, phone = Pickup.Phone,
                    address = NewAddressField ? "" : WrongRecoveredAddress ? "Different street" : Pickup.Address,
                    address_v2 = NewAddressField ? Pickup.Address : "", is_new_address = true, province_id_v2 = 1000001, ward_id_v2 = 1000100 }];
                if (SecondPage && request.RequestUri.Query.Contains("offset=0&"))
                    return Json(JsonSerializer.Serialize(new { code = 200, data = new { last_offset = 999, shops = Enumerable.Range(1, 100).Select(id => new { _id = id, name = "Other", status = 1 }) } }));
                if (SecondPage) Assert.Contains("offset=999", request.RequestUri.Query);
                return Json(JsonSerializer.Serialize(new { code = 200, data = new { last_offset = 456, shops = registered.Prepend(new { _id = 123, name = "Bootstrap", status = 1 }) } }));
            }
            Assert.Equal(SecondPage ? "1" : "123", Assert.Single(request.Headers.GetValues("ShopId")));
            if (path.EndsWith("/province/all"))
                return Json("""{"code":200,"data":[{"_id":1000001,"name":"Hồ Chí Minh","extension_names":["thành phố hồ chí minh"],"status":1}]}""");
            if (path.EndsWith("/ward/all-by-province-id"))
            {
                Assert.Contains("province_id=1000001", request.RequestUri.Query);
                return Json("""{"code":200,"data":[{"_id":1000100,"parent_id":1000001,"name":"Phường Sài Gòn","status":1}]}""");
            }
            Assert.EndsWith("/shop/register", path);
            Assert.Equal(HttpMethod.Post, request.Method);
            var body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(ct));
            Assert.True(body.GetProperty("is_new_address").GetBoolean());
            Assert.Equal(1000001, body.GetProperty("province_id_v2").GetInt32());
            Assert.Equal(1000100, body.GetProperty("ward_id_v2").GetInt32());
            Assert.False(body.TryGetProperty("district_id", out _));
            Assert.False(body.TryGetProperty("ward_code", out _));
            Assert.Equal(Pickup.Address, body.GetProperty("address").GetString());
            Assert.Equal(Pickup.Address, body.GetProperty("address_v2").GetString());
            Assert.Equal(Pickup.Phone, body.GetProperty("phone").GetString());
            if (FailCreate) return Json("""{"code":400,"code_message":"PHONE_INVALID","message":"secret details"}""", HttpStatusCode.BadRequest);
            Name = body.GetProperty("name").GetString(); Creates++;
            if (LoseResponse) throw new HttpRequestException("lost response after creation");
            return Json(CreateResponse);
        }
    }

    [Fact]
    public async Task RegistersItsOwnTwoLevelPickupAndRecoversTheSameShopOnRetry()
    {
        using var provider = new Provider();
        using var http = new HttpClient(provider);
        var client = new GhnQuoteClient(http, Config());
        Assert.Equal(456, await client.GetOrRegisterShopAsync(StoreId, Pickup, default));
        Assert.Equal(456, await client.GetOrRegisterShopAsync(StoreId, Pickup, default));
        Assert.Equal(1, provider.Creates);
        Assert.Contains(StoreId.ToString("N"), provider.Name);
    }

    [Fact]
    public async Task LostResponseAndPaginationDoNotCreateAnotherShop()
    {
        using var provider = new Provider { LoseResponse = true };
        using var http = new HttpClient(provider);
        var client = new GhnQuoteClient(http, Config());
        await Assert.ThrowsAsync<BusinessException>(() => client.GetOrRegisterShopAsync(StoreId, Pickup, default));
        provider.LoseResponse = false; provider.SecondPage = true;
        Assert.Equal(456, await client.GetOrRegisterShopAsync(StoreId, Pickup, default));
        Assert.Equal(1, provider.Creates);
    }

    [Fact]
    public async Task DoesNotBindARecoveredShopWithDifferentAddress()
    {
        using var provider = new Provider();
        using var http = new HttpClient(provider);
        var client = new GhnQuoteClient(http, Config());
        await client.GetOrRegisterShopAsync(StoreId, Pickup, default);
        provider.WrongRecoveredAddress = true;
        await Assert.ThrowsAsync<BusinessException>(() => client.GetOrRegisterShopAsync(StoreId, Pickup, default));
        Assert.Equal(1, provider.Creates);
    }

    [Fact]
    public async Task MissingCredentialsNeverCallsTheProvider()
    {
        using var http = new HttpClient(new Handler(_ => throw new InvalidOperationException("Unexpected call")));
        var error = await Assert.ThrowsAsync<BusinessException>(() => new GhnQuoteClient(http, Config(null)).GetOrRegisterShopAsync(StoreId, Pickup, default));
        Assert.Equal(503, error.StatusCode);
    }

    [Fact]
    public async Task InvalidPickupPhoneIsActionableWithoutExposingProviderDetails()
    {
        using var provider = new Provider { FailCreate = true };
        using var http = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<BusinessException>(() => new GhnQuoteClient(http, Config()).GetOrRegisterShopAsync(StoreId, Pickup, default));
        Assert.Equal(422, error.StatusCode);
        Assert.Contains("điện thoại", error.Message);
        Assert.DoesNotContain("secret", error.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"code\":200,\"data\":null}")]
    [InlineData("{\"code\":\"200\",\"data\":{}}")]
    [InlineData("{\"code\":200,\"data\":{\"shops\":[],\"last_offset\":0}}")]
    public async Task InvalidOrEmptyProviderDataFailsClosed(string body)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Json(body))));
        var error = await Assert.ThrowsAsync<BusinessException>(() => new GhnQuoteClient(http, Config()).GetOrRegisterShopAsync(StoreId, Pickup, default));
        Assert.Equal(503, error.StatusCode);
    }

    [Fact]
    public async Task UnknownWardDoesNotCreateAShop()
    {
        using var provider = new Provider();
        using var http = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<BusinessException>(() => new GhnQuoteClient(http, Config()).GetOrRegisterShopAsync(StoreId, Pickup with { Ward = "Unknown ward" }, default));
        Assert.Equal(422, error.StatusCode);
        Assert.Equal(0, provider.Creates);
    }

    [Theory]
    [InlineData("{\"code\":200,\"data\":{\"shop_id\":0}}")]
    [InlineData("{\"code\":200,\"data\":{\"shop_id\":\"456\"}}")]
    [InlineData("{\"code\":200,\"data\":null}")]
    [InlineData("{\"code\":200,\"data\":{}}")]
    [InlineData("not json")]
    public async Task InvalidRegistrationResultIsNeverAccepted(string body)
    {
        using var provider = new Provider { CreateResponse = body };
        using var http = new HttpClient(provider);
        var error = await Assert.ThrowsAsync<BusinessException>(() => new GhnQuoteClient(http, Config()).GetOrRegisterShopAsync(StoreId, Pickup, default));
        Assert.Equal(503, error.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransportFailureOrTimeoutIsSafeToRetry(bool timeout)
    {
        using var http = new HttpClient(new Handler(_ => throw (timeout ? new TaskCanceledException("provider details") : new HttpRequestException("provider details"))));
        var error = await Assert.ThrowsAsync<BusinessException>(() => new GhnQuoteClient(http, Config()).GetOrRegisterShopAsync(StoreId, Pickup, default));
        Assert.Equal(503, error.StatusCode);
        Assert.DoesNotContain("provider details", error.Message);
    }

    [Fact]
    public async Task CallerCancellationIsNotReportedAsProviderFailure()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        using var http = new HttpClient(new Handler(_ => throw new TaskCanceledException()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GhnQuoteClient(http, Config()).GetOrRegisterShopAsync(StoreId, Pickup, cancellation.Token));
    }

    [Theory]
    [InlineData("+84901234567")]
    [InlineData("84901234567")]
    public async Task AcceptsNormalizedPhoneAndProviderTwoLevelAddressField(string phone)
    {
        using var provider = new Provider { NewAddressField = true };
        using var http = new HttpClient(provider);
        Assert.Equal(456, await new GhnQuoteClient(http, Config()).GetOrRegisterShopAsync(StoreId, Pickup with { Phone = phone }, default));
    }
}
