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
using BCOBCA.API.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;

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
            Assert.Empty(await db.RefreshTokens.ToListAsync());
            Assert.All(await db.Accounts.ToListAsync(), account => Assert.Null(account.LastLoginAt));
            foreach (var platform in new[] { "web", "native" })
            {
                var context = new DefaultHttpContext();
                if (platform == "native") context.Request.Headers["X-Client-Platform"] = "native";
                var controller = new AuthController(auth, new RegistrationEnvironment(), config) { ControllerContext = new ControllerContext { HttpContext = context } };
                var response = Assert.IsType<ObjectResult>(await controller.Register(new RegisterRequest($"{platform}@example.test", password, "Registration API user"), default));
                Assert.Equal(201, response.StatusCode);
                var payload = JsonSerializer.SerializeToElement(response.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                Assert.Equal(new[] { "message" }, payload.GetProperty("data").EnumerateObject().Select(p => p.Name));
                Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
            }
            Assert.Empty(await db.RefreshTokens.ToListAsync());
            Assert.NotEqual(first.User.Id, second.User.Id);
            Assert.Equal("", first.User.Email);
            Assert.Equal("0901234567", first.User.Phone);
            Assert.Equal(first.User.Id, (await auth.LoginAsync(new LoginRequest("0901234567", password), default)).User.Id);
            Assert.Single(await db.RefreshTokens.ToListAsync());
            Assert.Equal(second.User.Id, (await auth.LoginAsync(new LoginRequest("+84901234568", password), default)).User.Id);
            var email = await auth.RegisterAsync(new RegisterRequest(" EMAIL@EXAMPLE.COM ", password, "Email user"), default);
            Assert.Equal("email@example.com", email.User.Email);
            Assert.Equal(email.User.Id, (await auth.LoginAsync(new LoginRequest("email@example.com", password), default)).User.Id);
            Assert.NotNull(email.User.CreatedAt);
            Assert.Null(email.User.DateOfBirth);
            var edited = await auth.UpdateProfileAsync(email.User.AccountId, new UpdateProfileRequest("Updated name", "", "Public nickname", "https://example.test/avatar.png", "https://example.test/cover.png", 1, "2000-02-29"), default);
            Assert.Equal("https://example.test/cover.png", edited.CoverUrl);
            Assert.Equal(new DateOnly(2000, 2, 29), edited.DateOfBirth);
            Assert.Equal(1, edited.Gender);
            Assert.False(edited.PhoneVerified);
            var legacy = await auth.UpdateProfileAsync(email.User.AccountId, new UpdateProfileRequest("Legacy edit", "", edited.Nickname, edited.AvatarUrl), default);
            Assert.Equal(edited.CoverUrl, legacy.CoverUrl);
            Assert.Equal(edited.Gender, legacy.Gender);
            Assert.Equal(edited.DateOfBirth, legacy.DateOfBirth);
            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => auth.UpdateProfileAsync(email.User.AccountId, new UpdateProfileRequest("Name", "", Gender: 9), default));
            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => auth.UpdateProfileAsync(email.User.AccountId, new UpdateProfileRequest("Name", "", DateOfBirth: "2099-01-01"), default));
            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => auth.UpdateProfileAsync(email.User.AccountId, new UpdateProfileRequest("Name", "", CoverUrl: "http://example.test/image.png"), default));
            var referredUser = await db.Users.SingleAsync(u => u.Id == email.User.Id);
            referredUser.ReferredByUserId = first.User.Id;
            await db.SaveChangesAsync();
            Assert.Null((await auth.GetProfileAsync(email.User.AccountId, default)).ReferrerNickname);
            var referrer = await db.Users.SingleAsync(u => u.Id == first.User.Id);
            referrer.Nickname = "Public referrer";
            await db.SaveChangesAsync();
            Assert.Equal("Public referrer", (await auth.GetProfileAsync(email.User.AccountId, default)).ReferrerNickname);
            Assert.Null((await auth.UpdateProfileAsync(email.User.AccountId, new UpdateProfileRequest("Clear birthday", "", DateOfBirth: ""), default)).DateOfBirth);
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
    private sealed class RegistrationEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "RegistrationTests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
