using BCOBCA.BLL.Common;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerStoreProfileTests
{
    [Fact]
    public async Task OwnerCanPersistCompleteProfileWithoutChangingOwnershipOrVerification()
    {
        EnvironmentBootstrap.Load();
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure test PostgreSQL connection.");
        var database = "bcobca_test_profile_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { Database = database }.ConnectionString).Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.MigrateAsync();
            var owner = new Account { Email = "owner@example.test", Role = "Seller", Status = 1 };
            var other = new Account { Email = "other@example.test", Role = "Seller", Status = 1 };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Original", Slug = "original-store", StoreType = 1, Status = 1 };
            var category = new Category { Name = "Trà", Slug = "profile-tea", IsActive = true };
            db.AddRange(owner, other, shop, category); await db.SaveChangesAsync();
            var service = new SellerService(new CommerceStore(db));
            var initial = await service.ProfileAsync(owner.Id, shop.Id, default);
            Assert.Empty(initial.Branches);
            var input = SellerStoreProfileValidationTests.Input(initial.UpdatedAt) with { CategoryIds = [category.Id] };
            await Assert.ThrowsAsync<BusinessException>(() => service.ProfileAsync(other.Id, shop.Id, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.UpdateProfileAsync(other.Id, shop.Id, input, default));
            var updated = await service.UpdateProfileAsync(owner.Id, shop.Id, input, default);
            Assert.Equal(input.Name, updated.Name); Assert.Equal(input.Description, updated.Description);
            Assert.Equal(input.LogoUrl, updated.LogoUrl); Assert.Equal(input.CoverUrl, updated.CoverUrl);
            Assert.Equal(input.Email, updated.Email); Assert.Equal(input.Website, updated.Website);
            Assert.Equal(input.Settings, updated.Settings); Assert.Equal("Toàn quốc", updated.Provinces);
            Assert.Equal(category.Id, Assert.Single(updated.Categories).Id);
            Assert.Equal(input.MainBranch.Address, Assert.Single(updated.Branches).Address);
            Assert.Equal(input.MainBranch.Address, Assert.Single(await service.StoresAsync(owner.Id, default)).Address);
            await Assert.ThrowsAsync<BusinessException>(() => service.UpdateProfileAsync(owner.Id, shop.Id, input, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.UpdateProfileAsync(owner.Id, shop.Id, input with { UpdatedAt = updated.UpdatedAt, Name = "Rejected", CategoryIds = [Guid.NewGuid()] }, default));
            db.ChangeTracker.Clear();
            Assert.Equal(input.Name, (await service.ProfileAsync(owner.Id, shop.Id, default)).Name);
            var saved = await db.Stores.SingleAsync(s => s.Id == shop.Id);
            Assert.Equal(owner.Id, saved.OwnerAccountId); Assert.False(saved.IsVerified); Assert.Equal("original-store", saved.Slug);
            Assert.Equal(1, saved.Status); Assert.Null(saved.GhnTestShopId);
            var next = await service.UpdateProfileAsync(owner.Id, shop.Id, input with { UpdatedAt = updated.UpdatedAt, Name = "Updated again", CategoryIds = [] }, default);
            Assert.Empty(next.Categories); Assert.Single(next.Branches);
            var suspendedOwner = await db.Accounts.SingleAsync(a => a.Id == owner.Id); suspendedOwner.Status = 0; await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessException>(() => service.ProfileAsync(owner.Id, shop.Id, default));
            suspendedOwner.Status = 1; saved.Status = 0; await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessException>(() => service.UpdateProfileAsync(owner.Id, shop.Id, input with { UpdatedAt = next.UpdatedAt }, default));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
