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

public sealed class PhoneRegistrationFlowTests
{
    [Fact]
    public async Task PhoneAccountsCanRegisterAndLoginWithoutEmailAndRejectDuplicateContacts()
    {
        EnvironmentBootstrap.Load();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException("Set a PostgreSQL development connection to run integration tests.");
        var databaseName = "bcobca_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(connectionString) { Database = databaseName }.ConnectionString;
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var store = new CommerceStore(db);
            var auth = new AuthService(store, config, new RegisterValidator(), new LoginValidator(), new ProfileValidator());
            const string password = "StrongPassword123";
            var first = await auth.RegisterAsync(new RegisterRequest("", password, "First user", "0901234567"), default);
            var second = await auth.RegisterAsync(new RegisterRequest("", password, "Second user", "+84901234568"), default);
            Assert.NotEqual(first.User.Id, second.User.Id);
            Assert.Equal("", first.User.Email);
            Assert.Equal("0901234567", first.User.Phone);
            Assert.Equal(first.User.Id, (await auth.LoginAsync(new LoginRequest("0901234567", password), default)).User.Id);
            Assert.Equal(second.User.Id, (await auth.LoginAsync(new LoginRequest("+84901234568", password), default)).User.Id);
            var email = await auth.RegisterAsync(new RegisterRequest(" EMAIL@EXAMPLE.COM ", password, "Email user"), default);
            Assert.Equal("email@example.com", email.User.Email);
            Assert.Equal(email.User.Id, (await auth.LoginAsync(new LoginRequest("email@example.com", password), default)).User.Id);
            var duplicatePhone = await Assert.ThrowsAsync<BusinessException>(() => auth.RegisterAsync(new RegisterRequest("", password, "Duplicate", "0901234567"), default));
            Assert.Equal(409, duplicatePhone.StatusCode);
            var duplicateEmail = await Assert.ThrowsAsync<BusinessException>(() => auth.RegisterAsync(new RegisterRequest("EMAIL@EXAMPLE.COM", password, "Duplicate"), default));
            Assert.Equal(409, duplicateEmail.StatusCode);
            await Assert.ThrowsAsync<BusinessException>(() => auth.LoginAsync(new LoginRequest("0901234567", "WrongPassword123"), default));
            var verification = await Assert.ThrowsAsync<BusinessException>(() => new AccountSecurityService(store, config).SendVerificationAsync(first.User.AccountId, default));
            Assert.Contains("chưa có email", verification.Message);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
