using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerRegistrationTests
{
    [Fact]
    public async Task SellerSignupCreatesOwnedStoreAndCustomerSignupDoesNot()
    {
        EnvironmentBootstrap.Load();
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure test PostgreSQL connection.");
        var database = "bcobca_test_signup_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { Database = database }.ConnectionString).Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.MigrateAsync();
            var repository = new CommerceStore(db);
            var auth = new AuthService(repository, new ConfigurationBuilder().AddEnvironmentVariables().Build(), new RegisterValidator(), new LoginValidator(), new ProfileValidator());
            var seller = new SellerService(repository);
            const string password = "StrongPassword123!";
            foreach (var input in new[] { new RegisterRequest(" SELLER@EXAMPLE.TEST ", password, "Seller"), new RegisterRequest("", password, "Phone seller", "0901234567") })
            {
                var registered = await auth.RegisterSellerAsync(input, default);
                Assert.Contains("Seller", registered.User.Roles);
                Assert.DoesNotContain("Admin", registered.User.Roles);
                Assert.DoesNotContain("BrandOwner", registered.User.Roles);
                var shop = Assert.Single(await seller.StoresAsync(registered.User.AccountId, default));
                var stored = await db.Stores.SingleAsync(s => s.Id == shop.Id);
                Assert.Equal(1, stored.StoreType);
                Assert.Equal(1, stored.Status);
                Assert.False(stored.IsVerified);
                Assert.Equal(registered.User.Email, stored.Email);
                Assert.Equal(registered.User.Phone, stored.Phone);
                Assert.Equal("Seller", (await db.Accounts.SingleAsync(a => a.Id == registered.User.AccountId)).Role);
                Assert.True(await db.StoreSettings.AnyAsync(s => s.StoreId == shop.Id));
                Assert.Equal(shop.Id, (await seller.OnboardAsync(registered.User.AccountId, default)).Id);
                Assert.Single(await seller.StoresAsync(registered.User.AccountId, default));
                var login = await auth.LoginAsync(new(registered.User.Email == "" ? registered.User.Phone : registered.User.Email, password), default);
                Assert.Contains("Seller", login.User.Roles);
                Assert.Equal(0, (await seller.SummaryAsync(registered.User.AccountId, shop.Id, default)).TotalOrders);
            }
            var customer = await auth.RegisterAsync(new("customer@example.test", password, "Customer"), default);
            Assert.Equal(new[] { "Customer" }, customer.User.Roles);
            Assert.Empty(await seller.StoresAsync(customer.User.AccountId, default));
            await Assert.ThrowsAsync<BusinessException>(() => auth.RegisterSellerAsync(new("customer@example.test", password, "Duplicate"), default));
            Assert.Empty(await seller.StoresAsync(customer.User.AccountId, default));
            var recovered = await seller.OnboardAsync(customer.User.AccountId, default);
            Assert.Equal(recovered.Id, (await seller.OnboardAsync(customer.User.AccountId, default)).Id);
            Assert.Contains("Seller", (await auth.GetProfileAsync(customer.User.AccountId, default)).Roles);
            var blocked = await db.Stores.SingleAsync(s => s.Id == recovered.Id);
            blocked.Status = 0;
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessException>(() => seller.OnboardAsync(customer.User.AccountId, default));
            blocked.Status = 1;
            blocked.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessException>(() => seller.OnboardAsync(customer.User.AccountId, default));
            Assert.Equal(3, await db.Stores.IgnoreQueryFilters().CountAsync());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
