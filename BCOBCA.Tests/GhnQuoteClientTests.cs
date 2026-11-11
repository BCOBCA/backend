using System.Net;
using System.Text.Json;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BCOBCA.Tests;

public sealed class GhnQuoteClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => respond(request);
    }
    private static readonly UserAddress Address = new() { RecipientName = "Test customer", Phone = "0901234567", AddressLine = "Test address", ProvinceCode = "79", WardCode = "26734" };
    private static readonly GhnPackage Package = new(123, 500, 20, 15, 10, 250000, [new { name = "Test item", quantity = 1, weight = 500, length = 20, width = 15, height = 10 }]);
    private static IConfiguration Config(string? token = "test-token") => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Shipping:Ghn:TestToken"] = token }).Build();

    [Fact]
    public async Task CallsOnlyStagingPreviewWithItsStoreAndDoesNotCreateAnOrder()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(async request => {
            calls++; Assert.Equal(GhnQuoteClient.PreviewUrl, request.RequestUri!.AbsoluteUri);
            Assert.Equal("dev-online-gateway.ghn.vn", request.RequestUri.Host);
            Assert.Equal("test-token", Assert.Single(request.Headers.GetValues("Token")));
            Assert.Equal("123", Assert.Single(request.Headers.GetValues("ShopId")));
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync()).RootElement;
            Assert.Equal(500, body.GetProperty("weight").GetInt32());
            Assert.True(body.GetProperty("is_new_to_address").GetBoolean());
            Assert.Equal(250000, body.GetProperty("cod_amount").GetDecimal());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"code":200,"data":{"order_code":"","total_fee":23000,"expected_delivery_time":"2026-11-15T12:00:00Z"}}""") };
        }));
        var rate = await new GhnQuoteClient(http, Config()).QuoteAsync(Address, "Hồ Chí Minh", "Phường Sài Gòn", Package, default);
        Assert.Equal(23000m, rate.Fee); Assert.NotNull(rate.ExpectedDeliveryAt); Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("{\"code\":200,\"data\":{\"total_fee\":-1}}")]
    [InlineData("{\"code\":400,\"data\":null}")]
    [InlineData("{\"code\":200,\"data\":{}}")]
    [InlineData("{\"code\":\"200\",\"data\":{\"total_fee\":23000}}")]
    [InlineData("{\"code\":200,\"data\":{\"total_fee\":23000,\"order_code\":\"unexpected-order\"}}")]
    public async Task RejectsInvalidQuotesInsteadOfUsingAnInventedFlatFee(string json)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) })));
        await Assert.ThrowsAsync<BusinessException>(() => new GhnQuoteClient(http, Config()).QuoteAsync(Address, "Hồ Chí Minh", "Phường Sài Gòn", Package, default));
    }

    [Fact]
    public async Task MissingTestCredentialsNeverMakesANetworkCall()
    {
        using var http = new HttpClient(new Handler(_ => throw new InvalidOperationException("Unexpected network request")));
        await Assert.ThrowsAsync<BusinessException>(() => new GhnQuoteClient(http, Config(null)).QuoteAsync(Address, "Hồ Chí Minh", "Phường Sài Gòn", Package, default));
    }

    [Fact]
    public async Task AddressLookupNeverSendsTheCarrierTokenToTheLocationService()
    {
        using var http = new HttpClient(new Handler(request => {
            Assert.Equal("provinces.open-api.vn", request.RequestUri!.Host);
            Assert.False(request.Headers.Contains("Token"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"code":79,"name":"Hồ Chí Minh","wards":[{"code":26734,"name":"Phường Sài Gòn"}]}""") });
        }));
        var names = await new GhnQuoteClient(http, Config()).AddressNamesAsync(Address, default);
        Assert.Equal("Phường Sài Gòn", names.Ward);
    }
}
