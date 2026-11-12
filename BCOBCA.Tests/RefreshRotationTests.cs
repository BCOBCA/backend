using System.Security.Cryptography;
using System.Text;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class RefreshRotationTests
{
    [Fact]
    public async Task RecentRotationConflictKeepsTheNewSessionButOldReplayStillRevokesIt()
    {
        EnvironmentBootstrap.Load();
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure isolated PostgreSQL tests.");
        var scratch = new NpgsqlConnectionStringBuilder(supplied) { Database = "bcobca_refresh_rotation_" + Guid.NewGuid().ToString("N") };
        await using var admin = new NpgsqlConnection(supplied); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{scratch.Database}\" TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'vi-VN'", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var account = new Account { Email = "rotation@example.test", Status = 1 };
            account.PasswordHash = new PasswordHasher<Account>().HashPassword(account, "StrongPassword123!");
            var user = new User { AccountId = account.Id, ReferralCode = "ROTATION" };
            db.AddRange(account, user, new AccountRole { AccountId = account.Id, Role = "Customer" }); await db.SaveChangesAsync();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["Jwt:SigningKey"] = new string('k', 64), ["Jwt:Issuer"] = "rotation-test", ["Jwt:Audience"] = "rotation-test", ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "30"
            }).Build();
            var auth = new AuthService(new CommerceStore(db), config, new RegisterValidator(), new LoginValidator(), new ProfileValidator());
            var original = await auth.LoginAsync(new LoginRequest(account.Email, "StrongPassword123!"), default);
            var rotated = await auth.RefreshAsync(original.RefreshToken, default);
            var conflict = await Assert.ThrowsAsync<BusinessException>(() => auth.RefreshAsync(original.RefreshToken, default));
            Assert.Equal(409, conflict.StatusCode);
            var continued = await auth.RefreshAsync(rotated.RefreshToken, default);
            Assert.Equal(user.Id, continued.User.Id);
            Assert.NotEqual(rotated.RefreshToken, continued.RefreshToken);

            var oldHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(original.RefreshToken)));
            var old = await db.RefreshTokens.SingleAsync(t => t.TokenHash == oldHash);
            old.RevokedAt = DateTimeOffset.UtcNow.AddSeconds(-10); await db.SaveChangesAsync();
            Assert.Equal(401, (await Assert.ThrowsAsync<BusinessException>(() => auth.RefreshAsync(original.RefreshToken, default))).StatusCode);
            Assert.Equal(401, (await Assert.ThrowsAsync<BusinessException>(() => auth.RefreshAsync(continued.RefreshToken, default))).StatusCode);

            var signedOut = await auth.LoginAsync(new LoginRequest(account.Email, "StrongPassword123!"), default);
            await auth.LogoutAsync(signedOut.RefreshToken, default);
            Assert.Equal(401, (await Assert.ThrowsAsync<BusinessException>(() => auth.RefreshAsync(signedOut.RefreshToken, default))).StatusCode);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{scratch.Database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
