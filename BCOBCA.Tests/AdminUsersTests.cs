using BCOBCA.API.Controllers;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;
public sealed class AdminUsersTests
{
    [Fact]
    public void EndpointsRequireAdministrators() => Assert.Contains(typeof(AdminUsersController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(), a => a.Roles == "Admin,SuperAdmin");

    [Fact]
    public async Task AccountsAreScopedPagedAndStatusChangesAreAtomicAndAudited()
    {
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Use isolated PostgreSQL.");
        var scratch = new NpgsqlConnectionStringBuilder(supplied) { Database = "bcobca_users_" + Guid.NewGuid().ToString("N") };
        await using var connection = new NpgsqlConnection(supplied); await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{scratch.Database}\" TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'vi-VN'", connection)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var now = DateTimeOffset.UtcNow;
            Account Account(string email, string role = "Customer", short status = 1) => new() { Email = email, Role = role, Status = status, CreatedAt = now.AddDays(-3), UpdatedAt = now.AddDays(-1) };
            var admin = Account("admin@example.test", "Admin");
            var owner = Account("owner@example.test"); owner.Phone = "0912345678"; owner.EmailVerified = true;
            var other = Account("other@example.test", status: 0);
            var noProfile = Account("no-profile@example.test");
            var deleted = Account("deleted@example.test"); deleted.IsDeleted = true;
            var profile = new User { AccountId = owner.Id, FullName = "Nguyễn Minh Anh", Nickname = "Anh", ReferralCode = "USEROWNER", DateOfBirth = new DateOnly(1995, 5, 10) };
            var otherProfile = new User { AccountId = other.Id, FullName = "Other", ReferralCode = "USEROTHER" };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "User shop", Slug = "user-shop", StoreType = 1, Status = 1, CreatedAt = now, UpdatedAt = now };
            var foreignShop = new Store { OwnerAccountId = noProfile.Id, Name = "Other shop", Slug = "user-other-shop", StoreType = 1, Status = 1, CreatedAt = now, UpdatedAt = now };
            var brand = new Brand { OwnerAccountId = owner.Id, Name = "Brand", Slug = "users-brand", Status = 1 };
            var category = new Category { Name = "Category", Slug = "users-category", IsActive = true };
            var product = new Product { StoreId = shop.Id, BrandId = brand.Id, CategoryId = category.Id, Name = "Product", Slug = "users-product", SKU = "USER-SP", BasePrice = 10000, Status = 1 };
            db.AddRange(admin, owner, other, noProfile, deleted, profile, otherProfile, shop, foreignShop, brand, category, product);
            for (var i = 0; i < 23; i++)
            {
                var order = new Order { UserId = profile.Id, OrderCode = $"USER-{i}", Status = i == 0 ? "CANCELLED" : "COMPLETED", PaymentStatus = "PAID", Subtotal = 10000, TotalAmount = 10000, IdempotencyKey = $"user-order-{i}", CreatedAt = now.AddMinutes(-i) };
                var item = new OrderItem { OrderId = order.Id, StoreId = shop.Id, ProductId = product.Id, BrandId = brand.Id, ProductName = product.Name, Quantity = 1, UnitPrice = 10000, TotalPrice = 10000 };
                db.AddRange(order, item,
                    new ProductReview { UserId = profile.Id, ProductId = product.Id, StoreId = shop.Id, OrderItemId = item.Id, Rating = 5, Content = $"Review {i}", Status = (short)(i == 0 ? 0 : 1), CreatedAt = now.AddMinutes(-i) },
                    new AuditLog { AccountId = admin.Id, EntityType = "Account", EntityId = owner.Id, Action = "UPDATE_ACCOUNT", Details = $"History {i}", CreatedAt = now.AddMinutes(-i) });
            }
            db.AddRange(new Order { UserId = otherProfile.Id, OrderCode = "FOREIGN", Status = "COMPLETED", PaymentStatus = "PAID", TotalAmount = 999999, IdempotencyKey = "user-foreign-order", CreatedAt = now },
                new AuditLog { AccountId = admin.Id, EntityType = "Account", EntityId = other.Id, Action = "FOREIGN", CreatedAt = now },
                new RefreshToken { AccountId = owner.Id, TokenHash = "user-token-1", FamilyId = Guid.NewGuid(), ExpiresAt = now.AddDays(1) },
                new RefreshToken { AccountId = owner.Id, TokenHash = "user-token-2", FamilyId = Guid.NewGuid(), ExpiresAt = now.AddDays(1) });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var service = new AdminUserService(new CommerceStore(db));
            var summary = await service.SummaryAsync(admin.Id, default);
            Assert.Equal(3, summary.Total); Assert.Equal(2, summary.Active); Assert.Equal(1, summary.Locked); Assert.Equal(1, summary.Verified);
            Assert.Equal(owner.Id, Assert.Single((await service.ListAsync(admin.Id, new(Search: "minh anh"), default)).Items).Id);
            Assert.Equal(owner.Id, Assert.Single((await service.ListAsync(admin.Id, new(Search: "0912345678", Verification: "verified"), default)).Items).Id);
            Assert.Single((await service.ListAsync(admin.Id, new(Status: "locked"), default)).Items);
            Assert.Equal(2, (await service.ListAsync(admin.Id, new(Verification: "unverified"), default)).TotalItems);
            Assert.Equal(3, (await service.ListAsync(admin.Id, new(Sort: "name"), default)).TotalItems);
            Assert.False((await service.DetailAsync(admin.Id, owner.Id, new(), default)).Reviews.Items.First().Visible);
            Assert.DoesNotContain((await service.ListAsync(admin.Id, new(), default)).Items, a => a.Id == admin.Id || a.Id == deleted.Id);
            var detail = await service.DetailAsync(admin.Id, owner.Id, new(OrdersPage: 3, ReviewsPage: 3, HistoryPage: 3), default);
            Assert.Equal(23, detail.Stats.Orders); Assert.Equal(220000, detail.Stats.CompletedValue); Assert.Equal(1, detail.Stats.Stores); Assert.Equal(23, detail.Stats.Reviews);
            Assert.Equal(3, detail.Orders.Items.Count); Assert.Equal(3, detail.Reviews.Items.Count); Assert.Equal(3, detail.History.Items.Count);
            Assert.DoesNotContain(detail.Orders.Items, o => o.Code == "FOREIGN");
            Assert.Equal(shop.Id, Assert.Single(detail.Stores.Items).Id);
            Assert.Equal(new DateOnly(1995, 5, 10), detail.DateOfBirth);
            var purchased = await db.Orders.SingleAsync(o => o.OrderCode == "USER-1");
            purchased.RecipientName = "Recipient"; purchased.RecipientPhone = "0901234567"; purchased.ShippingAddress = "Delivery address";
            purchased.Subtotal = 30000; purchased.DiscountAmount = 2000; purchased.ShippingFee = 15000; purchased.ShippingDiscountAmount = 5000; purchased.TotalAmount = 38000;
            db.Add(new OrderItem { OrderId = purchased.Id, StoreId = foreignShop.Id, ProductId = product.Id, BrandId = brand.Id, ProductName = "Second shop item", Quantity = 2, UnitPrice = 10000, TotalPrice = 20000 });
            db.Add(new Payment { OrderId = purchased.Id, PaymentMethod = "COD", Status = "PAID", Amount = 38000, CreatedAt = now, PaidAt = now });
            db.Add(new Shipment { OrderId = purchased.Id, Carrier = "GHN", TrackingCode = "TEST-TRACK", Status = "delivered" });
            db.Add(new OrderStatusHistory { OrderId = purchased.Id, OldStatus = "SHIPPING", NewStatus = "COMPLETED", Note = "Delivered", CreatedAt = now });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var purchasedDetail = await service.OrderAsync(admin.Id, owner.Id, purchased.Id, default);
            Assert.Equal(owner.Id, purchasedDetail.UserId); Assert.Equal(purchased.Id, purchasedDetail.Id);
            Assert.Equal(2, purchasedDetail.ItemCount); Assert.Equal(2, purchasedDetail.Items.Count);
            Assert.Contains(purchasedDetail.Items, i => i.Name == "Second shop item");
            Assert.Equal(38000, purchasedDetail.TotalAmount); Assert.Equal(15000, purchasedDetail.ShippingFee); Assert.Equal(5000, purchasedDetail.ShippingDiscount);
            Assert.Equal("Recipient", purchasedDetail.RecipientName); Assert.Equal("COD", purchasedDetail.PaymentMethod);
            Assert.Equal("TEST-TRACK", Assert.Single(purchasedDetail.Shipments).TrackingCode); Assert.Single(purchasedDetail.History);
            var purchaseRow = (await service.DetailAsync(admin.Id, owner.Id, new(), default)).Orders.Items.Single(o => o.Id == purchased.Id);
            Assert.Equal(2, purchaseRow.ItemCount); Assert.Equal(3, purchaseRow.Quantity); Assert.Equal("COD", purchaseRow.PaymentMethod); Assert.Equal("Recipient", purchaseRow.RecipientName);
            Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => service.OrderAsync(admin.Id, other.Id, purchased.Id, default))).StatusCode);
            Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => service.OrderAsync(admin.Id, noProfile.Id, purchased.Id, default))).StatusCode);
            Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => service.OrderAsync(admin.Id, owner.Id, Guid.NewGuid(), default))).StatusCode);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.OrderAsync(owner.Id, owner.Id, purchased.Id, default))).StatusCode);
            var empty = await service.DetailAsync(admin.Id, noProfile.Id, new(), default);
            Assert.Empty(empty.Orders.Items); Assert.Null(empty.DateOfBirth); Assert.Equal("no-profile@example.test", empty.User.FullName);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.SummaryAsync(owner.Id, default))).StatusCode);
            Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => service.DetailAsync(admin.Id, admin.Id, new(), default))).StatusCode);
            await Assert.ThrowsAsync<BusinessException>(() => service.ListAsync(admin.Id, new(PageSize: 100), default));
            await Assert.ThrowsAsync<BusinessException>(() => service.DetailAsync(admin.Id, owner.Id, new(HistoryPage: 0), default));
            await Assert.ThrowsAsync<BusinessException>(() => service.StatusAsync(admin.Id, owner.Id, new(false, "", detail.User.UpdatedAt), default));
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.StatusAsync(owner.Id, noProfile.Id, new(false, "Forbidden", empty.User.UpdatedAt), default))).StatusCode);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.StatusAsync(admin.Id, admin.Id, new(false, "Protected", admin.UpdatedAt), default))).StatusCode);
            Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => service.StatusAsync(admin.Id, owner.Id, new(false, "Stale", now.AddDays(-9)), default))).StatusCode);
            Assert.All(await db.RefreshTokens.Where(t => t.AccountId == owner.Id).ToListAsync(), t => Assert.Null(t.RevokedAt));
            await service.StatusAsync(admin.Id, owner.Id, new(false, "Account suspended after review", detail.User.UpdatedAt), default); db.ChangeTracker.Clear();
            detail = await service.DetailAsync(admin.Id, owner.Id, new(), default);
            Assert.False(detail.User.Active); Assert.Equal(0, (await db.Stores.SingleAsync(s => s.Id == shop.Id)).Status); Assert.Equal(1, (await db.Stores.SingleAsync(s => s.Id == foreignShop.Id)).Status);
            Assert.All(await db.RefreshTokens.Where(t => t.AccountId == owner.Id).ToListAsync(), t => Assert.NotNull(t.RevokedAt));
            Assert.Contains(detail.History.Items, h => h.Action == "SUSPEND_ACCOUNT" && h.Reason == "Account suspended after review");
            Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => service.StatusAsync(admin.Id, owner.Id, new(true, "Stale unlock", now.AddDays(-9)), default))).StatusCode);
            await service.StatusAsync(admin.Id, owner.Id, new(true, "Unlock after appeal", detail.User.UpdatedAt), default); db.ChangeTracker.Clear();
            Assert.True((await service.DetailAsync(admin.Id, owner.Id, new(), default)).User.Active);
            Assert.Equal(0, (await db.Stores.SingleAsync(s => s.Id == shop.Id)).Status);
            Assert.All(await db.RefreshTokens.Where(t => t.AccountId == owner.Id).ToListAsync(), t => Assert.NotNull(t.RevokedAt));
            var json = System.Text.Json.JsonSerializer.Serialize(await service.DetailAsync(admin.Id, owner.Id, new(), default), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            Assert.DoesNotContain("passwordHash", json); Assert.DoesNotContain("tokenHash", json);
        }
        finally { NpgsqlConnection.ClearAllPools(); await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{scratch.Database}\" WITH (FORCE)", connection); await drop.ExecuteNonQueryAsync(); }
    }
}
