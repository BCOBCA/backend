using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class OrderCancellationFlowTests
{
    [Fact]
    public async Task CancellationRecordsActorAndReasonNotifiesEachRecipientAndPreservesPendingBoundary()
    {
        EnvironmentBootstrap.Load();
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        var database = "bcobca_cancel_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { Database = database }.ConnectionString).Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.MigrateAsync();
            var owner = new Account { Email = "cancel-owner@example.test", PasswordHash = "test", Status = 1 };
            var buyerAccount = new Account { Email = "cancel-buyer@example.test", PasswordHash = "test", Status = 1 };
            var stranger = new Account { Email = "cancel-other@example.test", PasswordHash = "test", Status = 1 };
            var buyer = new User { AccountId = buyerAccount.Id, FullName = "Buyer", ReferralCode = "CANCEL-BUYER" };
            var otherBuyer = new User { AccountId = stranger.Id, FullName = "Other", ReferralCode = "CANCEL-OTHER" };
            var shop = new Store { OwnerAccountId = owner.Id, StoreType = 1, Status = 1, Name = "Shop", Slug = "cancel-shop" };
            var category = new Category { Name = "Test", Slug = "cancel-test", IsActive = true };
            var brand = new Brand { OwnerAccountId = owner.Id, Name = "Brand", Slug = "cancel-brand", Status = 1 };
            var product = new Product { StoreId = shop.Id, BrandId = brand.Id, CategoryId = category.Id, Name = "Test", Slug = "cancel-product", SKU = "CANCEL", BasePrice = 100000, Status = 1 };
            var variant = new ProductVariant { ProductId = product.Id, SKU = "CANCEL-1", VariantName = "Test", Price = 100000, StockQuantity = 20, IsActive = true };
            db.AddRange(owner, buyerAccount, stranger, buyer, otherBuyer, shop, category, brand, product, variant);
            await db.SaveChangesAsync();
            var repository = new CommerceStore(db);
            var config = new ConfigurationBuilder().Build();
            var orders = TestOrders.Create(repository, config, new FinanceService(repository, config));
            var seller = new SellerService(repository, orders: orders);

            async Task<Order> AddOrder(string code, string status = "PENDING", string payment = "UNPAID")
            {
                var order = new Order { UserId = buyer.Id, OrderCode = code, IdempotencyKey = code, Status = status, PaymentStatus = payment, CreatedAt = DateTimeOffset.UtcNow };
                db.Add(order);
                db.Add(new OrderItem { OrderId = order.Id, ProductId = product.Id, VariantId = variant.Id, BrandId = brand.Id, StoreId = shop.Id, ProductName = product.Name, SKU = variant.SKU, Quantity = 1 });
                db.Add(new SellerOrder { OrderId = order.Id, StoreId = shop.Id, BrandId = brand.Id, Status = status, CreatedAt = order.CreatedAt });
                db.Add(new InventoryReservation { OrderId = order.Id, VariantId = variant.Id, Quantity = 1, Status = "HELD", CreatedAt = order.CreatedAt, ExpiresAt = order.CreatedAt.AddDays(1) });
                db.Add(new Shipment { OrderId = order.Id, Carrier = "GHN", GhnShopId = 123, Status = "PENDING", RequestJson = "{}" });
                variant.ReservedQuantity++;
                await db.SaveChangesAsync();
                return order;
            }

            var customerOrder = await AddOrder("CANCEL-BUYER");
            await Assert.ThrowsAsync<BusinessException>(() => orders.CancelAsync(buyer.Id, customerOrder.Id, new(""), default));
            await Assert.ThrowsAsync<BusinessException>(() => orders.CancelAsync(otherBuyer.Id, customerOrder.Id, new(OrderCancellationReasons.BuyerChoices[0]), default));
            await orders.CancelAsync(buyer.Id, customerOrder.Id, new(OrderCancellationReasons.BuyerChoices[0]), default);
            Assert.Equal("CANCELLED", customerOrder.Status);
            Assert.Equal(0, variant.ReservedQuantity);
            Assert.Equal(20, variant.StockQuantity);
            var customerHistory = await db.Set<OrderStatusHistory>().SingleAsync(h => h.OrderId == customerOrder.Id);
            Assert.Equal(buyerAccount.Id, customerHistory.ChangedByAccountId);
            Assert.Equal("Người mua đã hủy đơn với lý do: Không còn nhu cầu mua", customerHistory.Note);
            Assert.Equal("Bạn đã hủy đơn thành công với lý do: Không còn nhu cầu mua", (await db.Set<Notification>().SingleAsync(n => n.TargetId == customerOrder.Id && n.RecipientAccountId == buyerAccount.Id)).Body);
            Assert.Equal("Khách hàng đã hủy đơn với lý do: Không còn nhu cầu mua", (await db.Set<Notification>().SingleAsync(n => n.TargetId == customerOrder.Id && n.RecipientAccountId == owner.Id)).Body);
            await orders.CancelAsync(buyer.Id, customerOrder.Id, new(OrderCancellationReasons.BuyerChoices[1]), default);
            Assert.Equal(1, await db.Set<OrderStatusHistory>().CountAsync(h => h.OrderId == customerOrder.Id));
            Assert.Equal("CANCELLED", (await db.Set<Shipment>().SingleAsync(s => s.OrderId == customerOrder.Id)).Status);

            var shopOrder = await AddOrder("CANCEL-SELLER");
            await Assert.ThrowsAsync<BusinessException>(() => seller.UpdateOrderStatusAsync(stranger.Id, shop.Id, shopOrder.Id, new("CANCELLED", Reason: "Hết hàng"), default));
            await Assert.ThrowsAsync<BusinessException>(() => seller.UpdateOrderStatusAsync(owner.Id, shop.Id, shopOrder.Id, new("CANCELLED"), default));
            var result = await seller.UpdateOrderStatusAsync(owner.Id, shop.Id, shopOrder.Id, new("CANCELLED", Reason: " Sản phẩm bị lỗi khi đóng gói "), default);
            Assert.Equal("CANCELLED", result.Status);
            Assert.Equal(0, variant.ReservedQuantity);
            var shopHistory = await db.Set<OrderStatusHistory>().SingleAsync(h => h.OrderId == shopOrder.Id);
            Assert.Equal(owner.Id, shopHistory.ChangedByAccountId);
            Assert.Equal("Cửa hàng đã hủy đơn với lý do: Sản phẩm bị lỗi khi đóng gói", shopHistory.Note);
            Assert.Equal("Cửa hàng đã hủy đơn của bạn với lý do: Sản phẩm bị lỗi khi đóng gói", (await db.Set<Notification>().SingleAsync(n => n.TargetId == shopOrder.Id && n.RecipientAccountId == buyerAccount.Id)).Body);
            Assert.Equal("Bạn đã hủy đơn của khách hàng với lý do: Sản phẩm bị lỗi khi đóng gói", (await db.Set<Notification>().SingleAsync(n => n.TargetId == shopOrder.Id && n.RecipientAccountId == owner.Id)).Body);
            Assert.Contains(result.History!, entry => entry.Note == shopHistory.Note);
            await seller.UpdateOrderStatusAsync(owner.Id, shop.Id, shopOrder.Id, new("CANCELLED", Reason: "Hết hàng"), default);
            Assert.Equal(1, await db.Set<OrderStatusHistory>().CountAsync(h => h.OrderId == shopOrder.Id));

            var mixed = await AddOrder("CANCEL-MIXED");
            db.Add(new OrderItem { OrderId = mixed.Id, ProductId = product.Id, BrandId = brand.Id, StoreId = null, ProductName = "Unscoped line", SKU = "MIXED", Quantity = 1 });
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessException>(() => seller.UpdateOrderStatusAsync(owner.Id, shop.Id, mixed.Id, new("CANCELLED", Reason: "Hết hàng"), default));
            Assert.Equal("PENDING", mixed.Status);

            var confirmed = await AddOrder("CANCEL-CONFIRM");
            await seller.UpdateOrderStatusAsync(owner.Id, shop.Id, confirmed.Id, new("CONFIRMED"), default);
            Assert.Contains("Đơn hàng của bạn đã được xác nhận.", (await db.Set<Notification>().SingleAsync(n => n.TargetId == confirmed.Id && n.RecipientAccountId == buyerAccount.Id)).Body);
            foreach (var (status, payment) in new[] { ("CONFIRMED", "UNPAID"), ("READY", "UNPAID"), ("SHIPPING", "UNPAID"), ("PENDING", "PAID") })
            {
                var blocked = await AddOrder($"BLOCK-{status}-{payment}", status, payment);
                await Assert.ThrowsAsync<BusinessException>(() => orders.CancelAsync(buyer.Id, blocked.Id, new(OrderCancellationReasons.BuyerChoices[0]), default));
                await Assert.ThrowsAsync<BusinessException>(() => seller.UpdateOrderStatusAsync(owner.Id, shop.Id, blocked.Id, new("CANCELLED", Reason: "Hết hàng"), default));
                Assert.Equal(status, blocked.Status);
                Assert.False(await db.Set<Notification>().AnyAsync(n => n.TargetId == blocked.Id));
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
