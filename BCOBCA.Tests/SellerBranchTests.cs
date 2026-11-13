using System.Net;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerBranchTests
{
    private sealed class Locations : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("provinces.open-api.vn", request.RequestUri!.Host);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"code":79,"name":"Hồ Chí Minh","wards":[{"code":26734,"name":"Phường Sài Gòn"}]}""") });
        }
    }

    [Fact]
    public async Task OwnerManagesBranchesAndOneWarehouseIndependentlyOfProfileEditing()
    {
        EnvironmentBootstrap.Load();
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure test PostgreSQL connection.");
        var database = "bcobca_test_branches_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { Database = database }.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var owner = new Account { Email = "branches-owner@example.test", Role = "Seller", Status = 1 };
            var other = new Account { Email = "branches-other@example.test", Role = "Seller", Status = 1 };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Shop", Slug = "branch-shop", StoreType = 1, Status = 1, GhnTestShopId = 123 };
            var otherShop = new Store { OwnerAccountId = other.Id, Name = "Other", Slug = "other-branch-shop", StoreType = 1, Status = 1 };
            var foreign = new StoreBranch { StoreId = otherShop.Id, Name = "Other branch", Address = "Not yours" };
            db.AddRange(owner, other, shop, otherShop, foreign); await db.SaveChangesAsync();
            using var http = new HttpClient(new Locations());
            var service = new SellerService(new CommerceStore(db), new GhnQuoteClient(http, new ConfigurationBuilder().Build()));
            var profile = await service.ProfileAsync(owner.Id, shop.Id, default);
            var input = new SellerBranchInput("Kho 1", "0901234567", "12 Nguyễn Huệ", "79", "26734", profile.UpdatedAt);
            await Assert.ThrowsAsync<BusinessException>(() => service.SaveBranchAsync(other.Id, shop.Id, null, input, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.SaveBranchAsync(owner.Id, shop.Id, foreign.Id, input, default));
            var first = await service.SaveBranchAsync(owner.Id, shop.Id, null, input, default);
            Assert.Single(first.Branches); Assert.True(first.Branches[0].IsMain); Assert.False(first.Branches[0].IsPickup);
            Assert.Equal("Hồ Chí Minh", first.Branches[0].ProvinceName);
            await Assert.ThrowsAsync<BusinessException>(() => service.SaveBranchAsync(owner.Id, shop.Id, null, input, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.SaveBranchAsync(owner.Id, shop.Id, null, input with { UpdatedAt = first.UpdatedAt, WardCode = "99999" }, default));
            var second = await service.SaveBranchAsync(owner.Id, shop.Id, null, input with { Name = "Kho 2", Address = "34 Nguyễn Huệ", UpdatedAt = first.UpdatedAt }, default);
            Assert.Equal(2, second.Branches.Count);
            await Assert.ThrowsAsync<BusinessException>(() => service.SelectPickupBranchAsync(owner.Id, shop.Id, new(foreign.Id, second.UpdatedAt), default));
            var branch = second.Branches.Single(b => b.Name == "Kho 2");
            var selected = await service.SelectPickupBranchAsync(owner.Id, shop.Id, new(branch.Id, second.UpdatedAt), default);
            Assert.Equal(branch.Id, Assert.Single(selected.Branches, b => b.IsPickup).Id);
            Assert.True(selected.Branches.Single(b => b.Name == "Kho 1").IsMain);
            var next = await service.SelectPickupBranchAsync(owner.Id, shop.Id, new(first.Branches[0].Id, selected.UpdatedAt), default);
            Assert.Equal(first.Branches[0].Id, Assert.Single(next.Branches, b => b.IsPickup).Id);
            var edited = await service.SaveBranchAsync(owner.Id, shop.Id, first.Branches[0].Id, input with { Address = "56 Nguyễn Huệ", UpdatedAt = next.UpdatedAt }, default);
            Assert.Equal("56 Nguyễn Huệ", edited.Branches.Single(b => b.IsPickup).Address);
            var returnProfile = await service.SelectReturnBranchAsync(owner.Id, shop.Id, new(branch.Id, edited.UpdatedAt), default);
            Assert.Equal(branch.Id, Assert.Single(returnProfile.Branches, b => b.IsReturn).Id);
            Assert.Equal(first.Branches[0].Id, Assert.Single(returnProfile.Branches, b => b.IsPickup).Id);
            var shared = await service.SelectReturnBranchAsync(owner.Id, shop.Id, new(first.Branches[0].Id, returnProfile.UpdatedAt), default);
            Assert.True(shared.Branches.Single(b => b.IsPickup).IsReturn);
            Assert.Equal(123, (await db.Stores.SingleAsync(s => s.Id == shop.Id)).GhnTestShopId);
            await Assert.ThrowsAsync<BusinessException>(() => service.SelectPickupBranchAsync(owner.Id, shop.Id, new(branch.Id, next.UpdatedAt), default));
            var inactive = await db.StoreBranches.SingleAsync(b => b.Id == branch.Id); inactive.IsActive = false; await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessException>(() => service.SelectPickupBranchAsync(owner.Id, shop.Id, new(branch.Id, shared.UpdatedAt), default));
            inactive.IsActive = true; inactive.IsPickup = true;
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal("23505", Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
            db.ChangeTracker.Clear();
            var otherReturn = await db.StoreBranches.SingleAsync(b => b.Id == branch.Id);
            otherReturn.IsActive = true; otherReturn.IsReturn = true;
            var duplicateReturn = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal("23505", Assert.IsType<PostgresException>(duplicateReturn.InnerException).SqlState);
            db.ChangeTracker.Clear();
            var warehouse = await db.StoreBranches.SingleAsync(b => b.StoreId == shop.Id && b.IsPickup);
            warehouse.IsActive = false;
            var disabled = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal("23514", Assert.IsType<PostgresException>(disabled.InnerException).SqlState);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
