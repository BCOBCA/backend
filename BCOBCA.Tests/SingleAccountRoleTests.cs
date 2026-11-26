using BCOBCA.DAL.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;

namespace BCOBCA.Tests;

public sealed class SingleAccountRoleTests
{
    [Fact]
    public async Task MigrationKeepsAccountRoleAndLoginRefreshProfileUseThatSingleRole()
    {
        EnvironmentBootstrap.Load();
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure PostgreSQL tests.");
        var scratch = new NpgsqlConnectionStringBuilder(supplied) { Database = "bcobca_single_role_" + Guid.NewGuid().ToString("N") };
        await using var admin = new NpgsqlConnection(supplied); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{scratch.Database}\" TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'vi-VN'", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261125070018_AddProductReviewStore");
            var store = new CommerceStore(db);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["Jwt:SigningKey"] = new string('k', 64), ["Jwt:Issuer"] = "single-role-test", ["Jwt:Audience"] = "single-role-test", ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "30"
            }).Build();
            var auth = new AuthService(store, config, new RegisterValidator(), new LoginValidator(), new ProfileValidator());
            const string password = "StrongPassword123!";
            var registered = await auth.RegisterAsync(new RegisterRequest("admin@example.test", password, "Admin"), default);
            var account = await db.Accounts.SingleAsync(a => a.Id == registered.User.AccountId);
            account.Role = "Admin"; await db.SaveChangesAsync();
            // Reproduce the reported disagreement: Accounts says Admin, legacy table says Customer.
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AccountRoles\" (\"Id\", \"AccountId\", \"Role\") VALUES ({Guid.NewGuid()}, {account.Id}, {"Customer"})");
            var before = await auth.LoginAsync(new(account.Email, password), default);
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal("Admin", (await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id)).Role);
            Assert.Equal(401, (await Assert.ThrowsAsync<BusinessException>(() => auth.RefreshAsync(before.RefreshToken, default))).StatusCode);
            db.ChangeTracker.Clear();
            var login = await auth.LoginAsync(new(account.Email, password), default);
            Assert.Equal(new[] { "Admin" }, login.User.Roles);
            var token = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);
            Assert.Equal(new[] { "Admin" }, token.Claims.Where(c => c.Type is "role" or "http://schemas.microsoft.com/ws/2008/06/identity/claims/role").Select(c => c.Value));
            Assert.Equal(new[] { "Admin" }, (await auth.RefreshAsync(login.RefreshToken, default)).User.Roles);
            Assert.Equal(new[] { "Admin" }, (await auth.GetProfileAsync(account.Id, default)).Roles);
            // Store onboarding must never replace an elevated account's role with Seller.
            await new SellerService(store).OnboardAsync(account.Id, default);
            Assert.Equal(new[] { "Admin" }, (await auth.GetProfileAsync(account.Id, default)).Roles);

            var second = await auth.RegisterAsync(new("second@example.test", password, "Second"), default);
            var actor = await db.Accounts.SingleAsync(a => a.Id == account.Id); actor.Role = "SuperAdmin"; await db.SaveChangesAsync();
            var roles = new AdminCatalogService(store);
            await Assert.ThrowsAsync<BusinessException>(() => roles.SetRoleAsync(actor.Id, new(actor.Id, "Admin", true), default));
            await Assert.ThrowsAsync<BusinessException>(() => roles.SetRoleAsync(actor.Id, new(actor.Id, "SuperAdmin", false), default));
            await roles.SetRoleAsync(actor.Id, new(second.User.AccountId, "Seller", true), default);
            Assert.Equal(new[] { "Seller" }, (await auth.GetProfileAsync(second.User.AccountId, default)).Roles);
            var secondLogin = await auth.LoginAsync(new("second@example.test", password), default);
            await roles.SetRoleAsync(actor.Id, new(second.User.AccountId, "Admin", true), default);
            Assert.Equal(new[] { "Admin" }, (await auth.GetProfileAsync(second.User.AccountId, default)).Roles);
            Assert.Equal(401, (await Assert.ThrowsAsync<BusinessException>(() => auth.RefreshAsync(secondLogin.RefreshToken, default))).StatusCode);
            await roles.SetRoleAsync(actor.Id, new(second.User.AccountId, "Seller", false), default);
            Assert.Equal(new[] { "Admin" }, (await auth.GetProfileAsync(second.User.AccountId, default)).Roles);
            await roles.SetRoleAsync(actor.Id, new(second.User.AccountId, "Admin", false), default);
            Assert.Equal(new[] { "Customer" }, (await auth.GetProfileAsync(second.User.AccountId, default)).Roles);

            await migrator.MigrateAsync("20261125070018_AddProductReviewStore");
            var restored = await db.Database.SqlQueryRaw<string>("SELECT \"Role\" AS \"Value\" FROM \"AccountRoles\" WHERE \"AccountId\" = {0}", actor.Id).SingleAsync();
            Assert.Equal("SuperAdmin", restored);
            await migrator.MigrateAsync();
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{scratch.Database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public void ModelUsesAccountsRoleWithoutAnAccountRolesTable()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=unused").Options);
        Assert.DoesNotContain(db.Model.GetEntityTypes(), entity => entity.GetTableName() == "AccountRoles");
        Assert.NotNull(db.Model.FindEntityType(typeof(BCOBCA.DAL.Entities.Account))!.FindProperty("Role"));
    }
}
