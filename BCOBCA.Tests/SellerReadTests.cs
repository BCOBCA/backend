using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using BCOBCA.BLL.Interfaces;
using System.Net;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerReadTests
{
    [Fact]
    public async Task EveryReadIsScopedToTheOwnerAndExcludesMixedStoreOrders()
    {
        EnvironmentBootstrap.Load();
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure test PostgreSQL connection.");
        var database = "bcobca_test_seller_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { Database = database }.ConnectionString).Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.MigrateAsync();
            var ownerA = new Account { Email = "seller-a@example.test", PasswordHash = "test", Status = 1 };
            var ownerB = new Account { Email = "seller-b@example.test", PasswordHash = "test", Status = 1 };
            var buyerAccount = new Account { Email = "buyer@example.test", PasswordHash = "test", Status = 1 };
            var buyer = new User { AccountId = buyerAccount.Id, FullName = "Buyer", ReferralCode = "SELLER-READ" };
            var shopA = new Store { OwnerAccountId = ownerA.Id, StoreType = 1, Status = 1, Name = "Shop A", Slug = "shop-a" };
            var shopB = new Store { OwnerAccountId = ownerB.Id, StoreType = 1, Status = 1, Name = "Shop B", Slug = "shop-b" };
            var brand = new Brand { OwnerAccountId = ownerA.Id, Name = "Brand", Slug = "brand", Status = 1 };
            var category = new Category { Name = "Tea", Slug = "tea", IsActive = true };
            var productA = new Product { StoreId = shopA.Id, BrandId = brand.Id, CategoryId = category.Id, Name = "Hidden tea", Slug = "hidden-tea", SKU = "A", BasePrice = 100000, Status = 0 };
            var productB = new Product { StoreId = shopB.Id, BrandId = brand.Id, CategoryId = category.Id, Name = "Other store tea", Slug = "other-tea", SKU = "B", BasePrice = 90000, Status = 1 };
            var variant = new ProductVariant { ProductId = productA.Id, SKU = "A-1", VariantName = "Tea", Price = 100000, StockQuantity = 10, ReservedQuantity = 3, IsActive = true };
            var completed = new Order { UserId = buyer.Id, OrderCode = "SELLER-DONE", Status = "COMPLETED", PaymentStatus = "PAID", Subtotal = 200000, DiscountAmount = 20000, ShippingFee = 30000, ShippingDiscountAmount = 10000, TotalAmount = 200000, IdempotencyKey = "seller-done", CreatedAt = DateTimeOffset.UtcNow };
            var mixed = new Order { UserId = buyer.Id, OrderCode = "SELLER-MIXED", Status = "COMPLETED", PaymentStatus = "PAID", TotalAmount = 999999, IdempotencyKey = "seller-mixed", CreatedAt = DateTimeOffset.UtcNow };
            db.AddRange(ownerA, ownerB, buyerAccount, buyer, shopA, shopB, brand, category, productA, productB, variant, completed, mixed);
            db.AddRange(
                new OrderItem { OrderId = completed.Id, ProductId = productA.Id, VariantId = variant.Id, BrandId = brand.Id, StoreId = shopA.Id, ProductName = "Historical tea", SKU = "A", Quantity = 2, UnitPrice = 100000, DiscountAmount = 20000, TotalPrice = 180000 },
                new OrderItem { OrderId = mixed.Id, ProductId = productA.Id, BrandId = brand.Id, StoreId = shopA.Id, ProductName = "A", SKU = "A", Quantity = 1, UnitPrice = 100000, TotalPrice = 100000 },
                new OrderItem { OrderId = mixed.Id, ProductId = productB.Id, BrandId = brand.Id, StoreId = shopB.Id, ProductName = "B", SKU = "B", Quantity = 1, UnitPrice = 90000, TotalPrice = 90000 });
            completed.CreatedAt = new DateTimeOffset(2026, 11, 18, 3, 0, 0, TimeSpan.Zero);
            completed.Note = "Gọi trước khi giao\nĐóng gói cẩn thận";
            completed.RecipientName = "Người nhận";
            completed.RecipientPhone = "0901234567";
            completed.ShippingAddress = "Địa chỉ đã lưu lúc đặt hàng";
            completed.ConfirmedAt = completed.CreatedAt.AddMinutes(5);
            completed.CompletedAt = completed.CreatedAt.AddDays(2);
            db.AddRange(new ProductImage { ProductId = productA.Id, ImageUrl = "https://example.test/tea.jpg", IsPrimary = true },
                new Payment { OrderId = completed.Id, PaymentMethod = "BANK_TRANSFER", Status = "PAID", Amount = completed.TotalAmount, Currency = "VND", CreatedAt = completed.CreatedAt, PaidAt = completed.CreatedAt.AddMinutes(1) },
                new Shipment { OrderId = completed.Id, Carrier = "GHN", TrackingCode = "GHN-123", Status = "DELIVERED", ShippedAt = completed.CreatedAt.AddDays(1), DeliveredAt = completed.CompletedAt },
                new OrderStatusHistory { OrderId = completed.Id, OldStatus = "SHIPPING", NewStatus = "COMPLETED", Note = "Người nhận đã nhận đủ hàng", CreatedAt = completed.CompletedAt.Value });
            await db.SaveChangesAsync();
            var service = new SellerService(new CommerceStore(db));
            Assert.Equal(shopA.Id, Assert.Single(await service.StoresAsync(ownerA.Id, default)).Id);
            var products = await service.ProductsAsync(ownerA.Id, shopA.Id, new CatalogQuery(), default);
            var product = Assert.Single(products.Items);
            Assert.Equal(productA.Id, product.Id);
            Assert.False(product.Active);
            Assert.Equal(7, product.Stock);
            var orders = await service.OrdersAsync(ownerA.Id, shopA.Id, new CatalogQuery(PageSize: 1), default);
            Assert.Equal(1, orders.TotalItems);
            var detail = await service.OrderAsync(ownerA.Id, shopA.Id, completed.Id, default);
            Assert.Equal(200000m, detail.Total);
            Assert.Equal(20000m, detail.Discount);
            Assert.Equal("Historical tea", Assert.Single(detail.Items).Name);
            Assert.Equal(completed.Note, detail.Note);
            Assert.Equal("PAID", detail.PaymentStatus);
            Assert.Equal("BANK_TRANSFER", detail.PaymentMethod);
            Assert.Equal(completed.ConfirmedAt, detail.ConfirmedAt);
            Assert.Equal(completed.CompletedAt, detail.CompletedAt);
            Assert.Equal("A", detail.Items[0].Sku);
            Assert.Equal("Tea", detail.Items[0].VariantName);
            Assert.Equal(20000m, detail.Items[0].Discount);
            Assert.Equal("https://example.test/tea.jpg", detail.Items[0].ImageUrl);
            Assert.Equal("GHN-123", Assert.Single(detail.Shipments!).TrackingCode);
            Assert.Equal("Người nhận đã nhận đủ hàng", Assert.Single(detail.History!).Note);
            Assert.Equal(completed.ShippingAddress, detail.Address);
            var summary = await service.SummaryAsync(ownerA.Id, shopA.Id, default);
            Assert.Equal(180000m, summary.Revenue);
            Assert.Equal(1, summary.TotalOrders);
            var forbidden = await Assert.ThrowsAsync<BusinessException>(() => service.ProductsAsync(ownerB.Id, shopA.Id, new CatalogQuery(), default));
            Assert.Equal(404, forbidden.StatusCode);
            await Assert.ThrowsAsync<BusinessException>(() => service.SummaryAsync(ownerB.Id, shopA.Id, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.OrderAsync(ownerB.Id, shopA.Id, completed.Id, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.OrderAsync(ownerA.Id, shopA.Id, mixed.Id, default));
            var pending = new Order { UserId = buyer.Id, OrderCode = "SELLER-PENDING", Status = "PENDING", PaymentStatus = "UNPAID", IdempotencyKey = "seller-pending", CreatedAt = DateTimeOffset.UtcNow };
            db.Add(pending);
            db.Add(new OrderItem { OrderId = pending.Id, ProductId = productA.Id, BrandId = brand.Id, StoreId = shopA.Id, ProductName = "Tea", SKU = "A", Quantity = 1 });
            db.Add(new SellerOrder { OrderId = pending.Id, StoreId = shopA.Id, BrandId = brand.Id, Status = "PENDING", CreatedAt = pending.CreatedAt });
            db.Add(new Payment { OrderId = pending.Id, PaymentMethod = "COD", Status = "PENDING", Amount = 0, Currency = "VND", CreatedAt = pending.CreatedAt });
            var queued = new Shipment { OrderId = pending.Id, Carrier = "GHN", Status = "PENDING", GhnShopId = 123, RequestJson = "{\"client_order_code\":\"SELLER-PENDING\",\"cod_amount\":0}" };
            db.Add(queued);
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessException>(() => service.UpdateOrderStatusAsync(ownerB.Id, shopA.Id, pending.Id, new("CONFIRMED"), default));
            await Assert.ThrowsAsync<BusinessException>(() => service.UpdateOrderStatusAsync(ownerA.Id, shopA.Id, mixed.Id, new("CONFIRMED"), default));
            await Assert.ThrowsAsync<BusinessException>(() => service.UpdateOrderStatusAsync(ownerA.Id, shopA.Id, pending.Id, new("COMPLETED", true), default));
            await Assert.ThrowsAsync<BusinessException>(() => service.UpdateOrderStatusAsync(ownerA.Id, shopA.Id, pending.Id, new("SHIPPING"), default));
            var confirmed = await service.UpdateOrderStatusAsync(ownerA.Id, shopA.Id, pending.Id, new("CONFIRMED"), default);
            Assert.Equal("CONFIRMED", confirmed.Status);
            Assert.NotNull(confirmed.ConfirmedAt);
            await Assert.ThrowsAsync<BusinessException>(() => service.UpdateOrderStatusAsync(ownerA.Id, shopA.Id, pending.Id, new("CONFIRMED"), default));
            await Assert.ThrowsAsync<BusinessException>(() => service.UpdateOrderStatusAsync(ownerA.Id, shopA.Id, pending.Id, new("SHIPPING"), default));
            var repository = new CommerceStore(db);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Shipping:Ghn:TestToken"] = "test-only" }).Build();
            var handler = new CarrierHandler();
            using var http = new HttpClient(handler);
            var client = new GhnQuoteClient(http, config);
            var orderService = new OrderService(repository, config, new FinanceService(repository, config), new GhnShippingQuoteService(repository, client));
            var fulfillment = new GhnFulfillmentService(repository, client, orderService);
            Assert.True(await fulfillment.CreateOneAsync(default));
            Assert.False(await fulfillment.CreateOneAsync(default));
            Assert.Equal(1, handler.Created);
            Assert.Equal("CONFIRMED", pending.Status);
            var at = DateTimeOffset.UtcNow.AddSeconds(-20);
            await Assert.ThrowsAsync<BusinessException>(() => fulfillment.CallbackAsync(new(999, "GHN-NEW", pending.OrderCode, "switch_status", "picked", at), default));
            await fulfillment.CallbackAsync(new(123, "GHN-NEW", pending.OrderCode, "switch_status", "picking", at), default);
            Assert.Equal("CONFIRMED", pending.Status);
            var picked = new GhnCallback(123, "GHN-NEW", pending.OrderCode, "switch_status", "picked", at.AddSeconds(1));
            await fulfillment.CallbackAsync(picked, default);
            var historyCount = await db.Set<OrderStatusHistory>().CountAsync(h => h.OrderId == pending.Id);
            await fulfillment.CallbackAsync(picked, default);
            await fulfillment.CallbackAsync(picked with { Status = "picking", Time = at }, default);
            Assert.Equal(historyCount, await db.Set<OrderStatusHistory>().CountAsync(h => h.OrderId == pending.Id));
            Assert.Equal("SHIPPING", pending.Status);
            Assert.Equal("SHIPPING", (await db.Set<SellerOrder>().SingleAsync(s => s.OrderId == pending.Id)).Status);
            Assert.Equal("UNPAID", pending.PaymentStatus);
            await fulfillment.CallbackAsync(picked with { Status = "delivery_fail", Time = at.AddSeconds(2) }, default);
            await fulfillment.CallbackAsync(picked with { Status = "delivered", Time = at.AddSeconds(3) }, default);
            Assert.Equal("SHIPPING", pending.Status); // A delivery callback alone does not prove COD collection.
            db.Add(new UserMembership { UserId = buyer.Id, TierId = await db.Set<MembershipTier>().OrderBy(t => t.SortOrder).Select(t => t.Id).FirstAsync(), MemberCode = "LIFECYCLE", JoinedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            handler.Collected = true;
            await fulfillment.CallbackAsync(picked with { Type = "cod", Status = "delivered", Time = at.AddSeconds(4) }, default);
            Assert.Equal("COMPLETED", pending.Status);
            Assert.Equal("PAID", pending.PaymentStatus);
            Assert.Equal("delivered", queued.Status);
            await fulfillment.CallbackAsync(picked with { Status = "picking", Time = at.AddSeconds(5) }, default);
            Assert.Equal("delivered", queued.Status);
            Assert.Single(await db.Set<PaymentTransaction>().Where(p => p.PaymentId == db.Set<Payment>().Where(p => p.OrderId == pending.Id).Select(p => p.Id).Single()).ToListAsync());
            var notificationCount = await db.Set<Notification>().CountAsync(n => n.RecipientAccountId == buyerAccount.Id && n.TargetId == pending.Id);
            await fulfillment.CallbackAsync(picked with { Type = "cod", Status = "delivered", Time = at.AddSeconds(4) }, default);
            Assert.Equal(notificationCount, await db.Set<Notification>().CountAsync(n => n.RecipientAccountId == buyerAccount.Id && n.TargetId == pending.Id));
            await Assert.ThrowsAsync<BusinessException>(() => service.UpdateOrderStatusAsync(ownerA.Id, shopA.Id, pending.Id, new("SHIPPING"), default));

            var protection = new EphemeralDataProtectionProvider();
            var notifications = new LiveNotificationService(repository, protection);
            var family = Guid.NewGuid();
            await notifications.RegisterAsync(buyerAccount.Id, family, new("ExpoPushToken[test-device]", "android"), true, default);
            var device = await db.Set<DeviceToken>().SingleAsync();
            Assert.DoesNotContain("test-device", device.ProtectedToken!);
            await using (var tx = await repository.BeginTransactionAsync(default))
            {
                await UserNotifications.AddAsync(repository, buyer.Id, "ORDER_STATUS", "Order", "Changed", "Order", pending.Id, default);
                await repository.SaveChangesAsync(); await tx.CommitAsync();
            }
            Assert.Single(await db.Set<PushDelivery>().ToListAsync());
            Assert.NotEmpty((await notifications.WaitAsync(buyerAccount.Id, null, default)).Items);
            Assert.DoesNotContain((await notifications.WaitAsync(ownerB.Id, null, default)).Items, n => n.TargetId == pending.Id);
            using var pushHttp = new HttpClient(new NoPushHandler());
            var pushes = new PushDeliveryService(repository, pushHttp, new EphemeralDataProtectionProvider(), config);
            Assert.True(await pushes.DeliverOneAsync(default)); // No live session: never send to a logged-out device.
            Assert.Equal("REVOKED", (await db.Set<PushDelivery>().SingleAsync()).Status);
            await notifications.RegisterAsync(buyerAccount.Id, family, new("ExpoPushToken[test-device]", "android"), false, default);
            Assert.False(device.Enabled);
            db.Add(new RefreshToken { AccountId = buyerAccount.Id, FamilyId = family, TokenHash = Guid.NewGuid().ToString(), CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });
            await db.SaveChangesAsync();
            await notifications.RegisterAsync(buyerAccount.Id, family, new("ExpoPushToken[test-device]", "android"), true, default);
            await UserNotifications.AddAsync(repository, buyer.Id, "ORDER_STATUS", "Order", "Push test", "Order", pending.Id, default);
            await repository.SaveChangesAsync();
            using var validPushHttp = new HttpClient(new PushHandler());
            var validPush = new PushDeliveryService(repository, validPushHttp, protection, config);
            Assert.True(await validPush.DeliverOneAsync(default));
            var ticket = await db.Set<PushDelivery>().SingleAsync(p => p.Status == "RECEIPT");
            Assert.Equal("ticket-test", ticket.TicketId);
            ticket.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
            Assert.True(await validPush.DeliverOneAsync(default));
            Assert.Equal("SENT", ticket.Status);
            await UserNotifications.AddAsync(repository, buyer.Id, "ORDER_STATUS", "Order", "Before switch", "Order", pending.Id, default);
            await repository.SaveChangesAsync();
            await notifications.RegisterAsync(ownerB.Id, Guid.NewGuid(), new("ExpoPushToken[test-device]", "android"), true, default);
            Assert.True(await pushes.DeliverOneAsync(default));
            Assert.Equal(2, await db.Set<PushDelivery>().CountAsync(p => p.Status == "REVOKED"));
            shopA.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            Assert.Empty(await service.StoresAsync(ownerA.Id, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.OrdersAsync(ownerA.Id, shopA.Id, new CatalogQuery(), default));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class CarrierHandler : HttpMessageHandler
    {
        public int Created { get; private set; }
        public bool Collected { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("dev-online-gateway.ghn.vn", request.RequestUri!.Host);
            object data;
            if (request.RequestUri.AbsolutePath.EndsWith("/create")) { Created++; data = new { order_code = "GHN-NEW" }; }
            else data = new { order_code = "GHN-NEW", shop_id = 123, status = "delivered", is_cod_collected = Collected, cod_amount = 0 };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { code = 200, data }), System.Text.Encoding.UTF8, "application/json") });
        }
    }
    private sealed class NoPushHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new InvalidOperationException("Revoked device must never receive push.");
    }
    private sealed class PushHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("exp.host", request.RequestUri!.Host);
            var payload = await request.Content!.ReadAsStringAsync(ct);
            var receipt = request.RequestUri.AbsolutePath.EndsWith("getReceipts");
            Assert.Contains(receipt ? "ticket-test" : "ExpoPushToken[test-device]", payload);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(receipt
                ? "{\"data\":{\"ticket-test\":{\"status\":\"ok\"}}}" : "{\"data\":{\"status\":\"ok\",\"id\":\"ticket-test\"}}", System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
