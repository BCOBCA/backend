using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class PublishedReviewsTests
{
    [Fact]
    public async Task DeliveredBuyerCanPublishImmediatelyWithoutSettlingCod()
    {
        await WithDatabase(async db =>
        {
            var buyer = new Account { Email = "buyer@review.test", Status = 1 };
            var owner = new Account { Email = "owner@review.test", Status = 1 };
            var user = new User { AccountId = buyer.Id, Nickname = "Buyer", ReferralCode = "REVIEWBUYER" };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Review Shop", Slug = "review-shop", StoreType = 1, Status = 1 };
            var brand = new Brand { OwnerAccountId = owner.Id, Name = "Brand", Slug = "review-brand", Status = 1 };
            var category = new Category { Name = "Category", Slug = "review-category", IsActive = true };
            var product = new Product { StoreId = shop.Id, BrandId = brand.Id, CategoryId = category.Id, Name = "Product", Slug = "review-product", SKU = "REVIEW", Status = 1 };
            var order = new Order { UserId = user.Id, OrderCode = "REVIEW001", Status = "SHIPPING", PaymentStatus = "UNPAID" };
            var item = new OrderItem { OrderId = order.Id, ProductId = product.Id, BrandId = brand.Id, StoreId = shop.Id, ProductName = product.Name, Quantity = 1 };
            db.AddRange(buyer, owner, user, shop, brand, category, product, order, item);
            db.AddRange(new DeviceToken { AccountId = owner.Id, TokenHash = "review-owner-test", Platform = "android", ProtectedToken = "test-only", Enabled = true },
                new DeviceToken { AccountId = buyer.Id, TokenHash = "review-buyer-test", Platform = "android", ProtectedToken = "test-only", Enabled = true });
            await db.SaveChangesAsync();
            var service = new ReviewService(new CommerceStore(db));
            await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(user.Id, item.Id, new(5, "Good product"), default));
            db.Add(new Shipment { OrderId = order.Id, Carrier = "GHN", TrackingCode = "TESTREVIEW", Status = "delivered", DeliveredAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            var id = await service.SaveAsync(user.Id, item.Id, new(5, "Good product"), default);
            var published = await service.ReviewsAsync(product.Id, new(), default);
            Assert.Equal(id, Assert.Single(published.Items).Id);
            Assert.Equal((short)1, published.Items[0].Status);
            Assert.Equal("UNPAID", order.PaymentStatus);
            Assert.Null(order.CompletedAt);
            var orderService = new OrderService(new CommerceStore(db), new ConfigurationBuilder().Build(), null!, null!);
            Assert.True((await orderService.OrderAsync(user.Id, order.Id, default)).CanReview);
            Assert.True(Assert.Single((await orderService.OrdersAsync(user.Id, new(), default)).Items).CanReview);
            Assert.True(Assert.Single((await orderService.OrderAsync(user.Id, order.Id, default)).Items).HasReview);
            Assert.True(Assert.Single(Assert.Single((await orderService.OrdersAsync(user.Id, new(), default)).Items).Items!).HasReview);
            var newReviewNotification = await db.Notifications.SingleAsync(n => n.Type == "REVIEW_CREATED");
            Assert.Equal(owner.Id, newReviewNotification.RecipientAccountId);
            Assert.Equal(shop.Id, newReviewNotification.TargetId);
            Assert.Equal("StoreReview", newReviewNotification.TargetType);
            Assert.Equal(1, await db.Set<PushDelivery>().CountAsync(p => p.NotificationId == newReviewNotification.Id));
            Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(user.Id, item.Id, new(5, "Good product"), default))).StatusCode);
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.Type == "REVIEW_CREATED"));
            await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(Guid.NewGuid(), item.Id, new(5, "Other buyer"), default));
            var listed = await service.SellerReviewsAsync(owner.Id, shop.Id, new(Rating: 5, Replied: false, Search: "review001"), default);
            var sellerReview = Assert.Single(listed.Items);
            Assert.Equal(order.Id, sellerReview.OrderId);
            Assert.Null(sellerReview.Reply);
            await Assert.ThrowsAsync<BusinessException>(() => service.SellerReviewsAsync(buyer.Id, shop.Id, new(), default));
            var foreignShop = new Store { OwnerAccountId = buyer.Id, Name = "Other", Slug = "other-review-shop", StoreType = 1, Status = 1 };
            db.Add(foreignShop); await db.SaveChangesAsync();
            Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => service.ReplyAsync(buyer.Id, foreignShop.Id, id, new("Wrong shop", sellerReview.Revision), default))).StatusCode);
            foreach (var invalid in new[] { " ", new string('a', 2001) })
                await Assert.ThrowsAsync<BusinessException>(() => service.ReplyAsync(owner.Id, shop.Id, id, new(invalid, sellerReview.Revision), default));
            var replied = await service.ReplyAsync(owner.Id, shop.Id, id, new("  Thank you!  ", sellerReview.Revision), default);
            Assert.Equal("Thank you!", replied.Reply?.Content);
            Assert.Equal(shop.Name, replied.Reply?.StoreName);
            var replyNotification = await db.Notifications.SingleAsync(n => n.Type == "REVIEW_REPLY");
            Assert.Equal(buyer.Id, replyNotification.RecipientAccountId);
            Assert.Equal(item.Id, replyNotification.TargetId);
            Assert.Equal("Review", replyNotification.TargetType);
            Assert.Equal(1, await db.Set<PushDelivery>().CountAsync(p => p.NotificationId == replyNotification.Id));
            await service.ReplyAsync(owner.Id, shop.Id, id, new("Thank you!", replied.Revision), default);
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.Type == "REVIEW_REPLY"));
            Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => service.ReplyAsync(owner.Id, shop.Id, id, new("Stale", sellerReview.Revision), default))).StatusCode);
            Assert.Empty((await service.SellerReviewsAsync(owner.Id, shop.Id, new(Replied: false), default)).Items);
            Assert.Single((await service.SellerReviewsAsync(owner.Id, shop.Id, new(Replied: true), default)).Items);
            Assert.Empty((await service.SellerReviewsAsync(owner.Id, shop.Id, new(Rating: 1), default)).Items);
            Assert.Empty((await service.SellerReviewsAsync(owner.Id, shop.Id, new(From: DateTimeOffset.UtcNow.AddDays(1)), default)).Items);
            Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(user.Id, item.Id, new(4, "Updated buyer review"), default))).StatusCode);
            var edited = Assert.Single((await service.ReviewsAsync(product.Id, new(), default)).Items);
            Assert.Equal(5, edited.Rating);
            Assert.Equal("Good product", edited.Content);
            Assert.Equal(replied.Revision, edited.Revision);
            Assert.Equal(1, edited.Status);
            Assert.Equal("Thank you!", edited.Reply?.Content);
            await service.ReplyAsync(owner.Id, shop.Id, id, new("Updated reply", edited.Revision), default);
            Assert.Equal("Updated reply", (await service.MyReviewAsync(user.Id, item.Id, default))?.Reply?.Content);
            order.Status = "CANCELLED"; await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(user.Id, item.Id, new(5, "Cancelled"), default));
            Assert.False((await orderService.OrderAsync(user.Id, order.Id, default)).CanReview);
            var vietnamNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
            Assert.Single((await service.SellerReviewsAsync(owner.Id, shop.Id, new(From: vietnamNow.AddDays(-1), To: vietnamNow.AddDays(1)), default)).Items);

            // Verify an actual upgrade publishes pending history but keeps rejected rows hidden.
            var secondItem = new OrderItem { OrderId = order.Id, ProductId = product.Id, BrandId = brand.Id, StoreId = shop.Id, Quantity = 1 };
            var hidden = new ProductReview { UserId = user.Id, OrderItemId = secondItem.Id, ProductId = product.Id, Rating = 1, Content = "Rejected", Status = 2 };
            db.AddRange(secondItem, hidden); await db.SaveChangesAsync();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261118074135_PushDeviceSessionBinding");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ProductReviews\" SET \"Status\" = 0 WHERE \"Id\" = {id}");
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            Assert.Equal(1, (await db.ProductReviews.SingleAsync(r => r.Id == id)).Status);
            Assert.Equal(2, (await db.ProductReviews.SingleAsync(r => r.Id == hidden.Id)).Status);
            Assert.False(db.Database.HasPendingModelChanges());
        });
    }

    private static async Task WithDatabase(Func<ApplicationDbContext, Task> action)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Set isolated PostgreSQL connection");
        var scratch = new NpgsqlConnectionStringBuilder(connection) { Database = "bcobca_reviews_" + Guid.NewGuid().ToString("N") };
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{scratch.Database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options);
            await db.Database.MigrateAsync();
            await action(db);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{scratch.Database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
