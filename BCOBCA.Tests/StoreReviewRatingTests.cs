using BCOBCA.BLL.Services;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace BCOBCA.Tests;

public sealed class StoreReviewRatingTests
{
    [Fact]
    public async Task OwnershipBackfillAndPublishedAverageUseAllIndividualShopReviews()
    {
        await SellerVerificationTests.WithDatabase(async options =>
        {
            await using var db = new ApplicationDbContext(options);
            var account = new Account { Email = "rating@test.local", Status = 1 };
            var user = new User { AccountId = account.Id, ReferralCode = "RATING" };
            var shop = new Store { OwnerAccountId = account.Id, Name = "Rating", Slug = "rating", StoreType = 1, Status = 1 };
            var other = new Store { OwnerAccountId = account.Id, Name = "Other", Slug = "rating-other", StoreType = 1, Status = 1 };
            var empty = new Store { OwnerAccountId = account.Id, Name = "Empty", Slug = "rating-empty", StoreType = 1, Status = 1 };
            var brand = new Brand { OwnerAccountId = account.Id, Name = "Brand", Slug = "rating-brand", Status = 1 };
            var category = new Category { Name = "Category", Slug = "rating-category", IsActive = true };
            var first = new Product { StoreId = shop.Id, BrandId = brand.Id, CategoryId = category.Id, Name = "First", Slug = "rating-first", SKU = "R1", Status = 1 };
            var second = new Product { StoreId = shop.Id, BrandId = brand.Id, CategoryId = category.Id, Name = "Second", Slug = "rating-second", SKU = "R2", Status = 0 };
            var foreign = new Product { StoreId = other.Id, BrandId = brand.Id, CategoryId = category.Id, Name = "Foreign", Slug = "rating-foreign", SKU = "R3", Status = 1 };
            var order = new Order { UserId = user.Id, OrderCode = "RATING001", Status = "COMPLETED", CompletedAt = DateTimeOffset.UtcNow };
            db.AddRange(account, user, shop, other, empty, brand, category, first, second, foreign, order);
            await db.SaveChangesAsync();
            var reviews = new ReviewService(new CommerceStore(db));
            foreach (var (product, rating, snapshot) in new[] { (first, 5, (Guid?)shop.Id), (first, 5, shop.Id), (first, 5, shop.Id), (second, 1, null), (foreign, 1, other.Id) })
            {
                var item = new OrderItem { OrderId = order.Id, ProductId = product.Id, BrandId = brand.Id, StoreId = snapshot, ProductName = product.Name, Quantity = 1 };
                db.Add(item); await db.SaveChangesAsync();
                var id = await reviews.SaveAsync(user.Id, item.Id, new((short)rating, "Review"), default);
                Assert.Equal(product.StoreId, (await db.Set<ProductReview>().SingleAsync(r => r.Id == id)).StoreId);
            }
            var hiddenItem = new OrderItem { OrderId = order.Id, ProductId = first.Id, BrandId = brand.Id, StoreId = shop.Id, ProductName = first.Name, Quantity = 1 };
            db.Add(hiddenItem);
            db.Add(new ProductReview { ProductId = first.Id, StoreId = shop.Id, OrderItemId = hiddenItem.Id, UserId = user.Id, Rating = 1, Content = "Hidden", Status = 2 });
            await db.SaveChangesAsync();
            var dealers = new DealerService(new CommerceStore(db));
            var result = (await dealers.DetailAsync(shop.Slug, null, default)).Dealer;
            Assert.Equal(4, result.ReviewCount);
            Assert.Equal(1, result.ProductCount);
            Assert.Equal(4d, result.AverageRating);
            var noReviews = (await dealers.DetailAsync(empty.Slug, null, default)).Dealer;
            Assert.Null(noReviews.AverageRating);
            Assert.Equal(0, noReviews.ReviewCount);
            Assert.Equal(0, noReviews.ProductCount);

            // The purchase snapshot remains authoritative if a product later belongs to another store.
            var movedItem = new OrderItem { OrderId = order.Id, ProductId = foreign.Id, BrandId = brand.Id, StoreId = shop.Id, ProductName = foreign.Name, Quantity = 1 };
            var pendingItem = new OrderItem { OrderId = order.Id, ProductId = first.Id, BrandId = brand.Id, StoreId = shop.Id, ProductName = first.Name, Quantity = 1 };
            db.AddRange(movedItem, pendingItem);
            db.Add(new ProductReview { ProductId = first.Id, StoreId = shop.Id, OrderItemId = pendingItem.Id, UserId = user.Id, Rating = 1, Content = "Pending", Status = 0 });
            await db.SaveChangesAsync();
            var movedReviewId = await reviews.SaveAsync(user.Id, movedItem.Id, new(4, "Purchased from original shop"), default);
            Assert.Equal(shop.Id, (await db.Set<ProductReview>().SingleAsync(r => r.Id == movedReviewId)).StoreId);
            result = (await dealers.DetailAsync(shop.Slug, null, default)).Dealer;
            Assert.Equal(5, result.ReviewCount);
            Assert.Equal(4d, result.AverageRating);

            var publishedReviews = await db.Set<ProductReview>().Where(r => r.StoreId == shop.Id && r.Status == 1).OrderBy(r => r.Id).ToListAsync();
            for (var i = 0; i < publishedReviews.Count; i++)
                publishedReviews[i].CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i);
            publishedReviews[0].ReplyContent = "Thank you for your review";
            publishedReviews[0].RepliedAt = DateTimeOffset.UtcNow;
            var legacyReview = publishedReviews.Single(r => r.ProductId == second.Id);
            legacyReview.ReplyContent = "Legacy purchase reply";
            legacyReview.RepliedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            var feed = await reviews.StoreReviewsAsync(shop.Id, new(PageSize: 2), default);
            Assert.Equal(5, feed.TotalItems);
            Assert.Equal(publishedReviews.Take(2).Select(r => r.Id), feed.Items.Select(r => r.Id));
            Assert.Equal(publishedReviews[0].ReplyContent, feed.Items[0].Reply?.Content);
            Assert.Equal(shop.Name, feed.Items[0].Reply?.StoreName);
            var page2 = await reviews.StoreReviewsAsync(shop.Id, new(Page: 2, PageSize: 2), default);
            var page3 = await reviews.StoreReviewsAsync(shop.Id, new(Page: 3, PageSize: 2), default);
            var all = feed.Items.Concat(page2.Items).Concat(page3.Items).ToArray();
            Assert.Equal(publishedReviews.Select(r => r.Id), all.Select(r => r.Id));
            Assert.Contains(all, r => r.ProductId == second.Id);
            Assert.Equal(shop.Name, all.Single(r => r.ProductId == second.Id).Reply?.StoreName);
            Assert.Empty((await reviews.StoreReviewsAsync(empty.Id, new(), default)).Items);
            Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => reviews.StoreReviewsAsync(Guid.NewGuid(), new(), default))).StatusCode);
            empty.Status = 0;
            await db.SaveChangesAsync();
            Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => reviews.StoreReviewsAsync(empty.Id, new(), default))).StatusCode);

            // Rolling this migration back drops ownership; applying it again must reconstruct it.
            var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(migrations[^2]);
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            Assert.Equal(7, await db.Set<ProductReview>().CountAsync(r => r.StoreId == shop.Id));
            Assert.Equal(1, await db.Set<ProductReview>().CountAsync(r => r.StoreId == other.Id));
            Assert.False(db.Database.HasPendingModelChanges());
        });
    }
}
