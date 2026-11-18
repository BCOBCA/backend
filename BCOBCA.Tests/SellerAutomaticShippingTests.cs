using System.Net;
using System.Text.Json;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerAutomaticShippingTests
{
    private sealed class Provider : DelegatingHandler
    {
        public GhnShopRegistrationTests.Provider Carrier { get; } = new();
        public Provider() { InnerHandler = Carrier; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => request.RequestUri!.Host == "provinces.open-api.vn"
                ? Task.FromResult(GhnShopRegistrationTests.Json("""{"code":79,"name":"Thành phố Hồ Chí Minh","wards":[{"code":26734,"name":"Phường Sài Gòn"}]}"""))
                : base.SendAsync(request, ct);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly NpgsqlConnection admin;
        private readonly string database = "bcobca_test_autoshipping_" + Guid.NewGuid().ToString("N");
        public DbContextOptions<ApplicationDbContext> Options { get; private set; } = null!;
        public Account Owner { get; } = new() { Email = "auto-shipping@example.test", Role = "Seller", Status = 1 };
        public Store Shop { get; private set; } = null!;
        public StoreBranch Branch { get; private set; } = null!;
        public Fixture()
        {
            EnvironmentBootstrap.Load();
            admin = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure test PostgreSQL connection."));
        }
        public async Task InitializeAsync(bool alreadySelected = false, int? shopId = null)
        {
            await admin.OpenAsync();
            await using (var cmd = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await cmd.ExecuteNonQueryAsync();
            Options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = database }.ConnectionString).Options;
            await using var db = new ApplicationDbContext(Options);
            await db.Database.MigrateAsync();
            Shop = new Store { Id = GhnShopRegistrationTests.StoreId, OwnerAccountId = Owner.Id, Name = "QQ", Slug = "auto-shipping-qq", StoreType = 1, Status = 1, GhnTestShopId = shopId };
            var pickup = GhnShopRegistrationTests.Pickup;
            Branch = new StoreBranch { StoreId = Shop.Id, Name = pickup.Name, Phone = pickup.Phone, Address = pickup.Address, ProvinceCode = "79", WardCode = "26734", ProvinceName = pickup.Province, WardName = pickup.Ward, IsPickup = alreadySelected };
            db.AddRange(Owner, Shop, Branch); await db.SaveChangesAsync();
            await db.Entry(Shop).ReloadAsync(); // PostgreSQL stores timestamps at microsecond precision.
        }
        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin);
            await cmd.ExecuteNonQueryAsync(); await admin.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectingOrSavingAnExistingPickupAutomaticallyPersistsItsShopId(bool alreadySelected)
    {
        await using var fixture = new Fixture(); await fixture.InitializeAsync(alreadySelected);
        using var provider = new Provider(); using var http = new HttpClient(provider);
        await using (var db = new ApplicationDbContext(fixture.Options))
        {
            var service = new SellerService(new CommerceStore(db), new GhnQuoteClient(http, GhnShopRegistrationTests.Config()));
            if (alreadySelected)
            {
                var b = fixture.Branch;
                await service.SaveBranchAsync(fixture.Owner.Id, fixture.Shop.Id, b.Id, new(b.Name, b.Phone, b.Address, b.ProvinceCode, b.WardCode, fixture.Shop.UpdatedAt), default);
            }
            else await service.SelectPickupBranchAsync(fixture.Owner.Id, fixture.Shop.Id, new(fixture.Branch.Id, fixture.Shop.UpdatedAt), default);
        }
        await using var saved = new ApplicationDbContext(fixture.Options);
        Assert.Equal(456, (await saved.Stores.SingleAsync()).GhnTestShopId);
        Assert.True((await saved.StoreBranches.SingleAsync()).IsPickup);
        Assert.Equal(1, provider.Carrier.Creates);
    }

    [Fact]
    public async Task ExistingBindingAndReturnSelectionNeverRegisterAnotherShop()
    {
        await using var fixture = new Fixture(); await fixture.InitializeAsync(shopId: 123);
        using var provider = new Provider(); using var http = new HttpClient(provider);
        await using var db = new ApplicationDbContext(fixture.Options);
        var service = new SellerService(new CommerceStore(db), new GhnQuoteClient(http, GhnShopRegistrationTests.Config()));
        var selected = await service.SelectPickupBranchAsync(fixture.Owner.Id, fixture.Shop.Id, new(fixture.Branch.Id, fixture.Shop.UpdatedAt), default);
        await service.SelectReturnBranchAsync(fixture.Owner.Id, fixture.Shop.Id, new(fixture.Branch.Id, selected.UpdatedAt), default);
        Assert.Equal(0, provider.Carrier.Calls);
        Assert.Equal(123, (await db.Stores.SingleAsync()).GhnTestShopId);
    }

    [Fact]
    public async Task LostCarrierResponseRollsBackSelectionAndRetryRecoversWithoutDuplicate()
    {
        await using var fixture = new Fixture(); await fixture.InitializeAsync();
        using var provider = new Provider(); provider.Carrier.LoseResponse = true;
        using var http = new HttpClient(provider);
        await using (var db = new ApplicationDbContext(fixture.Options))
        {
            var service = new SellerService(new CommerceStore(db), new GhnQuoteClient(http, GhnShopRegistrationTests.Config()));
            await Assert.ThrowsAsync<BusinessException>(() => service.SelectPickupBranchAsync(fixture.Owner.Id, fixture.Shop.Id, new(fixture.Branch.Id, fixture.Shop.UpdatedAt), default));
        }
        await using (var saved = new ApplicationDbContext(fixture.Options))
        {
            Assert.Null((await saved.Stores.SingleAsync()).GhnTestShopId);
            Assert.False((await saved.StoreBranches.SingleAsync()).IsPickup);
            Assert.Equal(fixture.Shop.UpdatedAt, (await saved.Stores.SingleAsync()).UpdatedAt);
        }
        provider.Carrier.LoseResponse = false;
        await using (var db = new ApplicationDbContext(fixture.Options))
        {
            var service = new SellerService(new CommerceStore(db), new GhnQuoteClient(http, GhnShopRegistrationTests.Config()));
            await service.SelectPickupBranchAsync(fixture.Owner.Id, fixture.Shop.Id, new(fixture.Branch.Id, fixture.Shop.UpdatedAt), default);
            Assert.Equal(456, (await db.Stores.SingleAsync()).GhnTestShopId);
        }
        Assert.Equal(1, provider.Carrier.Creates);
    }

    [Fact]
    public async Task UnauthorizedAndStaleSelectionsNeverCallCarrier()
    {
        await using var fixture = new Fixture(); await fixture.InitializeAsync();
        using var provider = new Provider(); using var http = new HttpClient(provider);
        await using var db = new ApplicationDbContext(fixture.Options);
        var service = new SellerService(new CommerceStore(db), new GhnQuoteClient(http, GhnShopRegistrationTests.Config()));
        await Assert.ThrowsAsync<BusinessException>(() => service.SelectPickupBranchAsync(Guid.NewGuid(), fixture.Shop.Id, new(fixture.Branch.Id, fixture.Shop.UpdatedAt), default));
        await Assert.ThrowsAsync<BusinessException>(() => service.SelectPickupBranchAsync(fixture.Owner.Id, fixture.Shop.Id, new(fixture.Branch.Id, fixture.Shop.UpdatedAt.AddDays(-1)), default));
        Assert.Equal(0, provider.Carrier.Calls);
    }

    [Fact]
    public async Task SelectingReturnAddressAloneDoesNotRegisterAShop()
    {
        await using var fixture = new Fixture(); await fixture.InitializeAsync();
        using var provider = new Provider(); using var http = new HttpClient(provider);
        await using var db = new ApplicationDbContext(fixture.Options);
        var service = new SellerService(new CommerceStore(db), new GhnQuoteClient(http, GhnShopRegistrationTests.Config()));
        await service.SelectReturnBranchAsync(fixture.Owner.Id, fixture.Shop.Id, new(fixture.Branch.Id, fixture.Shop.UpdatedAt), default);
        Assert.Equal(0, provider.Carrier.Calls);
        Assert.Null((await db.Stores.SingleAsync()).GhnTestShopId);
        Assert.True((await db.StoreBranches.SingleAsync()).IsReturn);
        Assert.False((await db.StoreBranches.SingleAsync()).IsPickup);
    }

    private static SellerBranchInput CreateInput(Fixture fixture, bool pickup, bool returns) => JsonSerializer.Deserialize<SellerBranchInput>(
        JsonSerializer.Serialize(new { name = "Kho mới", phone = GhnShopRegistrationTests.Pickup.Phone, address = GhnShopRegistrationTests.Pickup.Address,
            provinceCode = "79", wardCode = "26734", updatedAt = fixture.Shop.UpdatedAt, setAsPickup = pickup, setAsReturn = returns }), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CreatesAddressAndReplacesRequestedWarehouseRolesAtomically(bool pickup, bool returns)
    {
        await using var fixture = new Fixture(); await fixture.InitializeAsync(alreadySelected: true);
        await using var db = new ApplicationDbContext(fixture.Options);
        var previous = await db.StoreBranches.SingleAsync(); previous.IsReturn = true; await db.SaveChangesAsync();
        using var provider = new Provider(); using var http = new HttpClient(provider);
        var service = new SellerService(new CommerceStore(db), new GhnQuoteClient(http, GhnShopRegistrationTests.Config()));
        var result = await service.SaveBranchAsync(fixture.Owner.Id, fixture.Shop.Id, null, CreateInput(fixture, pickup, returns), default);
        var created = result.Branches.Single(b => b.Name == "Kho mới");
        Assert.Equal(pickup, created.IsPickup); Assert.Equal(returns, created.IsReturn);
        Assert.Equal(pickup ? created.Id : previous.Id, Assert.Single(result.Branches, b => b.IsPickup).Id);
        Assert.Equal(returns ? created.Id : previous.Id, Assert.Single(result.Branches, b => b.IsReturn).Id);
        Assert.Equal(pickup ? 1 : 0, provider.Carrier.Creates);
        Assert.Equal(pickup ? 456 : (int?)null, (await db.Stores.SingleAsync()).GhnTestShopId);
    }

    [Fact]
    public async Task FailedPickupCreationLeavesPreviousRolesAndBranchListUnchanged()
    {
        await using var fixture = new Fixture(); await fixture.InitializeAsync(alreadySelected: true);
        using var provider = new Provider(); provider.Carrier.FailCreate = true; using var http = new HttpClient(provider);
        await using (var db = new ApplicationDbContext(fixture.Options))
        {
            var service = new SellerService(new CommerceStore(db), new GhnQuoteClient(http, GhnShopRegistrationTests.Config()));
            await Assert.ThrowsAsync<BusinessException>(() => service.SaveBranchAsync(fixture.Owner.Id, fixture.Shop.Id, null, CreateInput(fixture, true, true), default));
        }
        await using var saved = new ApplicationDbContext(fixture.Options);
        Assert.Equal(fixture.Branch.Id, (await saved.StoreBranches.SingleAsync()).Id);
        Assert.True((await saved.StoreBranches.SingleAsync()).IsPickup);
        Assert.Null((await saved.Stores.SingleAsync()).GhnTestShopId);
    }

    [Fact]
    public async Task ConcurrentPickupSelectionsRegisterAtMostOneRemoteShop()
    {
        await using var fixture = new Fixture(); await fixture.InitializeAsync();
        using var provider = new Provider(); using var http = new HttpClient(provider);
        async Task<Exception?> Select()
        {
            await using var db = new ApplicationDbContext(fixture.Options);
            var service = new SellerService(new CommerceStore(db), new GhnQuoteClient(http, GhnShopRegistrationTests.Config()));
            return await Record.ExceptionAsync(() => service.SelectPickupBranchAsync(fixture.Owner.Id, fixture.Shop.Id, new(fixture.Branch.Id, fixture.Shop.UpdatedAt), default));
        }
        var results = await Task.WhenAll(Select(), Select());
        Assert.Single(results, error => error is null);
        Assert.Single(results, error => error is BusinessException { StatusCode: 409 } || error is not null && DatabaseConflicts.IsConcurrency(error));
        Assert.Equal(1, provider.Carrier.Creates);
    }
}
