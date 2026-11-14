using System.Net;
using System.Net.Http.Json;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerProductCreateHttpTests
{
    [Fact]
    public async Task SellerEndpointBindsCreateBodyAndIgnoresForgedOwnershipOrFeaturedFlags()
    {
        await SellerVerificationTests.WithDatabase(async options =>
        {
            var owner = new Account { Email = "product-http@example.test", Role = "Seller", Status = 1 };
            var other = new Account { Email = "product-http-other@example.test", Role = "Seller", Status = 1 };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Shop", Slug = "product-http", StoreType = 1, Status = 1 };
            var foreign = new Store { OwnerAccountId = other.Id, Name = "Other", Slug = "product-http-other", StoreType = 1, Status = 1 };
            var brand = new Brand { OwnerAccountId = other.Id, Name = "Brand", Slug = "product-http-brand", Status = 1 };
            var category = new Category { Name = "Category", Slug = "product-http-category", IsActive = true };
            await using (var db = new ApplicationDbContext(options)) { db.AddRange(owner, other, shop, foreign, brand, category); await db.SaveChangesAsync(); }
            var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders();
            builder.Services.AddScoped(_ => new ApplicationDbContext(options));
            builder.Services.AddScoped<ICommerceStore, CommerceStore>(); builder.Services.AddScoped<IAdminCatalogService, AdminCatalogService>();
            builder.Services.AddScoped<ISellerService, SellerService>();
            builder.Services.AddControllers().AddApplicationPart(typeof(SellerController).Assembly);
            builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, Authentication>("test", _ => { });
            builder.Services.AddAuthorization();
            await using var app = builder.Build(); app.UseMiddleware<ExceptionMiddleware>(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            var route = $"/api/seller/stores/{shop.Id}/products";
            var input = new SellerCreateProductInput(Guid.NewGuid(), brand.Id, category.Id, "New product", 50000, 3, ["https://images.example.test/item.png"], 100, 10, 10, 10);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(route, input)).StatusCode);
            client.DefaultRequestHeaders.Add("X-Test-Account", other.Id.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(route, input)).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Account"); client.DefaultRequestHeaders.Add("X-Test-Account", owner.Id.ToString());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/seller/stores/{shop.Id}/product-options")).StatusCode);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(route, input with { Price = 0 })).StatusCode);
            var body = JsonSerializer.SerializeToElement(input, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var forged = body.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
            forged["storeId"] = JsonSerializer.SerializeToElement(foreign.Id); forged["isFeatured"] = JsonSerializer.SerializeToElement(true);
            using var response = await client.PostAsJsonAsync(route, forged); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); var id = json.RootElement.GetProperty("data").GetProperty("id").GetGuid();
            Assert.Equal(3, json.RootElement.GetProperty("data").GetProperty("stock").GetInt32());
            using var retry = await client.PostAsJsonAsync(route, input); Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
            await using var check = new ApplicationDbContext(options); var product = await check.Products.SingleAsync();
            Assert.Equal(id, product.Id); Assert.Equal(shop.Id, product.StoreId); Assert.False(product.IsFeatured);
            Assert.Equal(1, await check.ProductVariants.CountAsync()); Assert.Equal(1, await check.Set<InventoryMovement>().CountAsync());
            var flexible = input with { RequestId = Guid.NewGuid(), SpecificationText = "Material: cotton\nCare: hand wash", Variants = [new("White / M", 60000, 2), new("White / L", 80000, 4, 70000)] };
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(route, flexible with { ImageUrls = Enumerable.Range(1, 6).Select(n => $"https://images.example.test/{n}.png").ToArray() })).StatusCode);
            using var flexibleResponse = await client.PostAsJsonAsync(route, flexible);
            Assert.Equal(HttpStatusCode.Created, flexibleResponse.StatusCode);
            using var flexibleJson = JsonDocument.Parse(await flexibleResponse.Content.ReadAsStringAsync());
            var flexibleData = flexibleJson.RootElement.GetProperty("data");
            Assert.Equal(2, flexibleData.GetProperty("variantCount").GetInt32());
            Assert.Equal(60000, flexibleData.GetProperty("price").GetDecimal());
            Assert.Equal(6, flexibleData.GetProperty("stock").GetInt32());
            var flexibleProductId = flexibleData.GetProperty("id").GetGuid();
            Assert.Equal(70000, (await check.ProductVariants.AsNoTracking().SingleAsync(v => v.ProductId == flexibleProductId && v.VariantName == "White / L")).SalePrice);
            Assert.Equal(flexible.SpecificationText, JsonDocument.Parse((await check.Products.AsNoTracking().SingleAsync(p => p.Id == flexibleProductId)).Specifications).RootElement.GetProperty("free_text").GetString());
            using var otherResponse = await client.PostAsJsonAsync(route, input with { RequestId = Guid.NewGuid(), BrandId = Guid.Empty, UseOtherBrand = true });
            Assert.Equal(HttpStatusCode.Created, otherResponse.StatusCode);
            Assert.Equal(1, await check.Brands.CountAsync(b => b.Slug == $"seller-other-{shop.Id:N}" && b.OwnerAccountId == owner.Id && !b.IsVerified));
            await app.StopAsync();
        });
    }

    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-Account"], out var accountId)) return Task.FromResult(AuthenticateResult.NoResult());
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", accountId.ToString())], Scheme.Name)), Scheme.Name)));
        }
    }
}
