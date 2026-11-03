using System.Security.Claims;
using BCOBCA.API.Controllers;
using BCOBCA.API.Services;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class BannerFlowTests
{
    [Fact]
    public async Task LegacyBannerMigrationPreservesImageTargetAndScheduleAndAdminCanPersistUploadedUrl()
    {
        EnvironmentBootstrap.Load();
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException("Configure a development PostgreSQL connection.");
        var scratch = new NpgsqlConnectionStringBuilder(supplied);
        var databaseName = "bcobca_banner_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(supplied);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            scratch.Database = databaseName;
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261102080632_GuardReviewRevisionAndShipmentIdentity");
            var id = Guid.NewGuid();
            var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var start = now.AddDays(-1);
            var end = now.AddDays(1);
            const string oldImage = "https://res.cloudinary.com/test-cloud/image/upload/legacy.png";
            const string oldTarget = "https://example.test/legacy-promotion";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Banners" ("Id", "Title", "ImageUrl", "TargetUrl", "Position", "SortOrder", "StartsAt", "EndsAt", "IsActive")
                VALUES ({id}, 'Legacy title', {oldImage}, {oldTarget}, 'HOME', 7, {start}, {end}, true)
                """);

            await migrator.MigrateAsync();

            var columns = await db.Database.SqlQueryRaw<string>("SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Banners'").ToListAsync();
            Assert.Equal(new[] { "EndsAt", "Id", "ImageUrl", "IsActive", "StartsAt", "TargetUrl" }, columns.Order());
            var legacy = await db.Banners.SingleAsync(x => x.Id == id);
            Assert.Equal(oldImage, legacy.ImageUrl);
            Assert.Equal(oldTarget, legacy.TargetUrl);
            Assert.Equal(start, legacy.StartsAt);
            Assert.Equal(end, legacy.EndsAt);
            Assert.True(legacy.IsActive);
            var catalog = new CatalogService(new CommerceStore(db));
            var publicBanner = Assert.Single(await catalog.BannersAsync(default));
            Assert.Equal(oldImage, publicBanner.ImageUrl);
            Assert.Equal(oldTarget, publicBanner.TargetUrl);

            var account = new Account { Email = "banner-admin@example.test", Phone = "0900000001", PasswordHash = "not-a-real-login", Role = "Admin", Status = 1, CreatedAt = now, UpdatedAt = now };
            db.Accounts.Add(account);
            db.AccountRoles.Add(new AccountRole { AccountId = account.Id, Role = "Admin" });
            await db.SaveChangesAsync();
            var service = new AdminCatalogService(new CommerceStore(db));
            const string uploadedUrl = "https://res.cloudinary.com/test-cloud/image/upload/bcobca/uploads/new.png";
            var createdId = await service.SaveBannerAsync(account.Id, null, new BannerInput(uploadedUrl, "https://example.test/new-promotion", now.AddHours(-1), end), default);
            var created = await db.Banners.SingleAsync(x => x.Id == createdId);
            Assert.Equal(uploadedUrl, created.ImageUrl);
            Assert.Equal("https://example.test/new-promotion", created.TargetUrl);

            var forbidden = await Assert.ThrowsAsync<BusinessException>(() => service.SaveBannerAsync(Guid.NewGuid(), null, new BannerInput(uploadedUrl, "", start, end), default));
            Assert.Equal(403, forbidden.StatusCode);
            var invalidSchedule = await Assert.ThrowsAsync<BusinessException>(() => service.SaveBannerAsync(account.Id, null, new BannerInput(uploadedUrl, "", end, start), default));
            Assert.Equal(422, invalidSchedule.StatusCode);

            using var noUploadHandler = new NoUploadHandler();
            using var uploadClient = new HttpClient(noUploadHandler);
            var uploadService = new CloudinaryImageService(uploadClient, new ConfigurationBuilder().Build());
            var controller = new UploadsController(uploadService, db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", account.Id.ToString()), new Claim(ClaimTypes.Role, "Admin") }, "test"))
                    }
                }
            };
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
            var image = new FormFile(new MemoryStream(png), 0, png.Length, "file", "image.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
            var missingConfiguration = await Assert.ThrowsAsync<BusinessException>(() => controller.Image(image, default));
            Assert.Equal(503, missingConfiguration.StatusCode);
            var assignedRole = await db.AccountRoles.SingleAsync(x => x.AccountId == account.Id && x.Role == "Admin");
            db.AccountRoles.Remove(assignedRole);
            await db.SaveChangesAsync();
            // The token still claims Admin, but the current database role has been revoked.
            var revoked = await Assert.ThrowsAsync<BusinessException>(() => controller.Image(image, default));
            Assert.Equal(403, revoked.StatusCode);
            Assert.Equal(0, noUploadHandler.Calls);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class NoUploadHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("Integration authorization test must never call Cloudinary.");
        }
    }
}
