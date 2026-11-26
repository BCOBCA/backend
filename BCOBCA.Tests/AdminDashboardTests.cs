using BCOBCA.BLL.Common;
using BCOBCA.BLL.Services;
using Xunit;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using BCOBCA.API.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace BCOBCA.Tests;

public sealed class AdminDashboardTests
{
    [Fact]
    public void DashboardEndpointRequiresAdminOrSuperAdmin()
        => Assert.Contains(typeof(AdminDashboardController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(), attribute => attribute.Roles == "Admin,SuperAdmin");

    [Fact]
    public async Task DashboardUsesVietnamBoundariesCompletedRevenueAndStoreScopedProducts()
    {
        EnvironmentBootstrap.Load();
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure isolated PostgreSQL.");
        var scratch = new NpgsqlConnectionStringBuilder(supplied) { Database = "bcobca_dashboard_" + Guid.NewGuid().ToString("N") };
        await using var connection = new NpgsqlConnection(supplied); await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{scratch.Database}\" TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'vi-VN'", connection)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var admin = new Account { Email = "dashboard-admin@example.test", Role = "Admin", Status = 1 };
            var buyer = new Account { Email = "dashboard-buyer@example.test", Role = "Customer", Status = 1 };
            var previousAccount = new Account { Email = "dashboard-old@example.test", Role = "Customer", Status = 1 };
            var from = new DateOnly(2026, 11, 19); var to = from.AddDays(6);
            var start = new DateTimeOffset(2026, 11, 18, 17, 0, 0, TimeSpan.Zero); var end = start.AddDays(7);
            var user = new User { AccountId = buyer.Id, FullName = "Người mua", ReferralCode = "DASHBOARD", CreatedAt = start };
            var previousUser = new User { AccountId = previousAccount.Id, ReferralCode = "DASHBOARD-OLD", CreatedAt = start.AddDays(-1) };
            var shop = new Store { OwnerAccountId = admin.Id, Name = "Dashboard store", Slug = "dashboard-store", StoreType = 1, Status = 1 };
            var brand = new Brand { OwnerAccountId = admin.Id, Name = "Dashboard brand", Slug = "dashboard-brand", Status = 1 };
            var category = new Category { Name = "Dashboard category", Slug = "dashboard-category", IsActive = true };
            var product = new Product { BrandId = brand.Id, CategoryId = category.Id, StoreId = shop.Id, Name = "Tai nghe", Slug = "dashboard-product", SKU = "DASHBOARD", BasePrice = 100000, Status = 1 };
            db.AddRange(admin, buyer, previousAccount, user, previousUser, shop, brand, category, product);
            Order Order(string status, DateTimeOffset at, decimal subtotal = 0, decimal discount = 0, string payment = "PAID") => new() {
                UserId = user.Id, Status = status, PaymentStatus = payment, CreatedAt = at, OrderCode = Guid.NewGuid().ToString("N"), IdempotencyKey = Guid.NewGuid().ToString("N"),
                Subtotal = subtotal, DiscountAmount = discount, ShippingFee = 10000, TotalAmount = subtotal - discount + 10000
            };
            var previous = Order("COMPLETED", start.AddSeconds(-1), 100000);
            var first = Order("COMPLETED", start, 220000, 20000);
            var last = Order("COMPLETED", end.AddSeconds(-1), 50000);
            var outside = Order("COMPLETED", end, 999999);
            var shipping = Order("SHIPPING", start.AddDays(2));
            db.AddRange(previous, Order("CANCELLED", start.AddDays(-2)), Order("PENDING", start.AddDays(-3)), first, last, outside, shipping, Order("PENDING", start.AddDays(1)), Order("CANCELLED", start.AddDays(3)), Order("COMPLETED", start.AddDays(4), 100000, 0, "REFUNDED"));
            foreach (var pair in new[] { (first, 2), (last, 3), (shipping, 99) }) db.Add(new OrderItem { OrderId = pair.Item1.Id, ProductId = product.Id, BrandId = brand.Id, StoreId = shop.Id, ProductName = product.Name, Quantity = pair.Item2, UnitPrice = 100000, TotalPrice = pair.Item2 * 100000 });
            await db.SaveChangesAsync();
            var service = new AdminDashboardService(new CommerceStore(db));
            var result = await service.GetAsync(admin.Id, from, to, default);
            Assert.Equal(250000, result.Revenue.Value); Assert.Equal(150, result.Revenue.ChangePercent);
            Assert.Equal(6, result.Orders.Value); Assert.Equal(100, result.Orders.ChangePercent);
            Assert.Equal(33.3m, result.CancellationRate.Value); Assert.Equal(0, result.CancellationRate.ChangePercent);
            Assert.Equal(1, result.NewUsers.Value); Assert.Equal(0, result.NewUsers.ChangePercent);
            Assert.Equal(1, result.ActiveStores.Value); Assert.Equal(1, result.ActiveProducts.Value); Assert.Null(result.ActiveStores.ChangePercent);
            Assert.Equal(7, result.Daily.Count); Assert.Equal(200000, result.Daily[0].Revenue); Assert.Equal(50000, result.Daily[^1].Revenue);
            Assert.Equal(6, result.Statuses.Sum(s => s.Count)); Assert.Equal(2, result.Statuses.Single(s => s.Key == "cancelled").Count);
            Assert.Equal(5, result.RecentOrders.Count); Assert.Equal("Người mua", result.RecentOrders[0].BuyerName);
            Assert.Equal(5, Assert.Single(result.TopProducts).Sold);
            Assert.Equal(shop.Name, result.RecentOrders.Single(o => o.Id == last.Id).StoreName);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(buyer.Id, from, to, default))).StatusCode);
            await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(admin.Id, to, from, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(admin.Id, from.AddDays(-100), to, default));
            var empty = await service.GetAsync(admin.Id, from.AddDays(-20), from.AddDays(-19), default);
            Assert.Equal(0, empty.Orders.Value); Assert.Empty(empty.RecentOrders); Assert.Empty(empty.TopProducts); Assert.Equal(2, empty.Daily.Count);
            admin.Status = 0; await db.SaveChangesAsync();
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(admin.Id, from, to, default))).StatusCode);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{scratch.Database}\" WITH (FORCE)", connection); await drop.ExecuteNonQueryAsync();
        }
    }

    [Theory]
    [InlineData(120, 100, 20)]
    [InlineData(75, 100, -25)]
    [InlineData(0, 100, -100)]
    public void ComparesEqualLengthPeriods(decimal current, decimal previous, decimal expected)
        => Assert.Equal(expected, AdminDashboardService.ChangePercent(current, previous));

    [Fact]
    public void DoesNotInventGrowthWithoutAPreviousBaseline()
        => Assert.Null(AdminDashboardService.ChangePercent(10, 0));

    [Theory]
    [InlineData("PENDING", "PAID", "pending")]
    [InlineData("CONFIRMED", "PAID", "processing")]
    [InlineData("READY", "PAID", "processing")]
    [InlineData("SHIPPING", "PAID", "shipping")]
    [InlineData("COMPLETED", "PAID", "completed")]
    [InlineData("COMPLETED", "REFUNDED", "cancelled")]
    [InlineData("CANCELLED", "UNPAID", "cancelled")]
    [InlineData("NEW_STATE", "PAID", "processing")]
    public void EveryOrderMapsToExactlyOneStatus(string status, string paymentStatus, string expected)
        => Assert.Equal(expected, AdminDashboardService.StatusBucket(status, paymentStatus));
}
