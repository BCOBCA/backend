using System.Text.Json;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class ReviewImagesJsonTests
{
    private const string Before = "20261111024124_StoreVoucherAndGhnTestShipping";

    [Fact]
    public async Task MigrationPreservesOrderedImagesAndServiceReplacesJsonWithoutChangingApi()
    {
        EnvironmentBootstrap.Load();
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure isolated PostgreSQL tests.");
        var scratch = new NpgsqlConnectionStringBuilder(supplied) { Database = "bcobca_review_json_" + Guid.NewGuid().ToString("N") };
        await using var admin = new NpgsqlConnection(supplied);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{scratch.Database}\" TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'vi-VN'", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(Before);
            var account = new Account { Email = "review-json@example.test", Status = 1 };
            var user = new User { AccountId = account.Id, Nickname = "Reviewer", ReferralCode = "REVIEW-JSON" };
            var brand = new Brand { OwnerAccountId = account.Id, Name = "Brand", Slug = "review-json", Status = 1 };
            var category = new Category { Name = "Category", Slug = "review-json" };
            db.AddRange(account, user, brand, category, new AccountRole { AccountId = account.Id, Role = "Admin" });
            var product = new Product { BrandId = brand.Id, CategoryId = category.Id, StoreId = TestStores.Own(db, brand), Name = "Product", Slug = "review-json", SKU = "REVIEW-JSON", Status = 1 };
            var order = new Order { UserId = user.Id, OrderCode = "REVIEW-JSON", Status = "COMPLETED", PaymentStatus = "PAID", CompletedAt = DateTimeOffset.UtcNow };
            var item = new OrderItem { OrderId = order.Id, ProductId = product.Id, BrandId = brand.Id, StoreId = product.StoreId, Quantity = 1 };
            var emptyItem = new OrderItem { OrderId = order.Id, ProductId = product.Id, BrandId = brand.Id, StoreId = product.StoreId, Quantity = 1 };
            db.AddRange(product, order, item, emptyItem);
            await db.SaveChangesAsync();
            var reviewId = Guid.NewGuid(); var emptyId = Guid.NewGuid(); var revision = Guid.NewGuid();
            foreach (var (id, itemId) in new[] { (reviewId, item.Id), (emptyId, emptyItem.Id) })
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "ProductReviews" ("Id", "ProductId", "UserId", "OrderItemId", "Rating", "Content", "Status", "Revision", "CreatedAt")
                    VALUES ({id}, {product.Id}, {user.Id}, {itemId}, 5, 'Existing review', 1, {revision}, now())
                    """);
            string[] urls = ["https://example.test/ảnh.png?name=\"first\"", "https://example.test/second.png", "https://example.test/last.png"];
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "ProductReviewImages" ("Id", "ProductReviewId", "ImageUrl", "SortOrder") VALUES
                ('00000000-0000-0000-0000-000000000003', {reviewId}, {urls[2]}, 10),
                ('00000000-0000-0000-0000-000000000002', {reviewId}, {urls[1]}, 0),
                ('00000000-0000-0000-0000-000000000001', {reviewId}, {urls[0]}, 0)
                """);

            await migrator.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal(urls, JsonSerializer.Deserialize<string[]>((await db.ProductReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId)).ImageUrls));
            Assert.Equal("[]", (await db.ProductReviews.AsNoTracking().SingleAsync(r => r.Id == emptyId)).ImageUrls);
            await using (var connection = new NpgsqlConnection(scratch.ConnectionString))
            {
                await connection.OpenAsync();
                await using var gone = new NpgsqlCommand("SELECT to_regclass('\"ProductReviewImages\"') IS NULL", connection);
                Assert.Equal(true, await gone.ExecuteScalarAsync());
            }

            await migrator.MigrateAsync(Before);
            await using (var connection = new NpgsqlConnection(scratch.ConnectionString))
            {
                await connection.OpenAsync();
                await using var restored = new NpgsqlCommand("SELECT array_agg(\"ImageUrl\" ORDER BY \"SortOrder\") FROM \"ProductReviewImages\" WHERE \"ProductReviewId\" = @id", connection);
                restored.Parameters.AddWithValue("id", reviewId);
                Assert.Equal(urls, Assert.IsType<string[]>(await restored.ExecuteScalarAsync()));
            }
            await migrator.MigrateAsync();
            var service = new ReviewService(new CommerceStore(db));
            Assert.Equal(urls, (await service.ReviewsAsync(product.Id, new CatalogQuery(), default)).Items.Single(r => r.Id == reviewId).ImageUrls);
            Assert.Equal(reviewId, await service.SaveAsync(user.Id, item.Id, new ReviewInput(5, "Existing review", urls), default));
            var unchanged = (await service.MyReviewAsync(user.Id, item.Id, default))!;
            Assert.Equal(revision, unchanged.Revision); Assert.Equal(1, unchanged.Status);
            string[] replacement = [urls[2], urls[0]];
            await service.SaveAsync(user.Id, item.Id, new ReviewInput(5, "Existing review", replacement), default);
            var changed = (await service.MyReviewAsync(user.Id, item.Id, default))!;
            Assert.Equal(replacement, changed.ImageUrls); Assert.Equal(0, changed.Status); Assert.NotEqual(revision, changed.Revision);
            Assert.Equal(replacement, (await service.PendingAsync(account.Id, new CatalogQuery(), default)).Items.Single().ImageUrls);
            await service.SaveAsync(user.Id, item.Id, new ReviewInput(5, "Existing review"), default);
            Assert.Empty((await service.MyReviewAsync(user.Id, item.Id, default))!.ImageUrls);
            Assert.Equal("[]", (await db.ProductReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId)).ImageUrls);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{scratch.Database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
