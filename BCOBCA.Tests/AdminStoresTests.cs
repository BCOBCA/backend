using BCOBCA.API.Controllers;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;
public sealed class AdminStoresTests
{
    [Fact]
    public void EndpointsRequireAdminRoles() => Assert.Contains(typeof(AdminStoresController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(), a => a.Roles == "Admin,SuperAdmin");

    [Fact]
    public async Task StoresAreScopedFilteredAndMutationsAreVersionedAndAudited()
    {
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Use isolated PostgreSQL.");
        var scratch = new NpgsqlConnectionStringBuilder(supplied) { Database = "bcobca_stores_" + Guid.NewGuid().ToString("N") };
        await using var connection = new NpgsqlConnection(supplied); await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{scratch.Database}\" TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'vi-VN'", connection)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var admin = new Account { Email = "stores-admin@example.test", Role = "Admin", Status = 1 };
            var owner = new Account { Email = "stores-owner@example.test", Role = "Customer", Status = 1 };
            var user = new User { AccountId = owner.Id, FullName = "Owner Search", Nickname = "Kai", ReferralCode = "STORES" };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Pending store", Slug = "pending-store", StoreType = 1, Status = 0, Phone = "0912345678", CreatedAt = DateTimeOffset.UtcNow.AddDays(-2) };
            var active = new Store { OwnerAccountId = owner.Id, Name = "Active store", Slug = "active-store", StoreType = 1, Status = 1, IsVerified = true, CreatedAt = DateTimeOffset.UtcNow.AddDays(-20) };
            var hidden = new Store { OwnerAccountId = owner.Id, Name = "Deleted", Slug = "deleted-store", StoreType = 1, Status = 1, DeletedAt = DateTimeOffset.UtcNow };
            var brandStore = new Store { OwnerAccountId = owner.Id, Name = "Other type", Slug = "other-type", StoreType = 0, Status = 1 };
            var verification = new StoreVerification { StoreId = shop.Id, Status = 0, LegalName = "Store Company", TaxCode = "1234567890", Documents = System.Text.Json.JsonSerializer.Serialize(new[] { new VerificationDocument("license", "license.png", "image/png", 3, "private-document") }) };
            var brand = new Brand { OwnerAccountId = owner.Id, Name = "Brand", Slug = "admin-stores-brand", Status = 1 };
            var order = new Order { UserId = user.Id, OrderCode = "STORES-ORDER", Status = "COMPLETED", PaymentStatus = "PAID", Subtotal = 1000000, TotalAmount = 1010000, IdempotencyKey = "admin-stores-order", CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) };
            var sellerOrder = new SellerOrder { StoreId = shop.Id, BrandId = brand.Id, OrderId = order.Id, TotalAmount = 40000, CreatedAt = order.CreatedAt };
            order.RecipientName = "Recipient Test";
            order.RecipientPhone = "0912345678";
            sellerOrder.Status = "SHIPPING";
            db.Add(new Payment { OrderId = order.Id, PaymentMethod = "COD", Amount = order.TotalAmount, Currency = "VND", Status = "PAID", CreatedAt = order.CreatedAt });
            db.AddRange(admin, owner, user, shop, active, hidden, brandStore, verification, brand, order, sellerOrder);
            var category = new Category { Name = "Reviews", Slug = "stores-review-category", IsActive = true };
            var product = new Product { Name = "Reviewed product", Slug = "stores-reviewed-product", SKU = "STORES-REVIEW", CategoryId = category.Id, BrandId = brand.Id, StoreId = shop.Id, BasePrice = 10000, Status = 1 };
            var otherProduct = new Product { Name = "Other product", Slug = "stores-other-reviewed-product", SKU = "STORES-OTHER-REVIEW", CategoryId = category.Id, BrandId = brand.Id, StoreId = active.Id, BasePrice = 10000, Status = 1 };
            OrderItem ReviewItem(Product p) => new() { OrderId = order.Id, ProductId = p.Id, BrandId = brand.Id, StoreId = p.StoreId, ProductName = p.Name, Quantity = 1, UnitPrice = 10000, TotalPrice = 10000 };
            var lowItem = ReviewItem(product); var highItem = ReviewItem(product); var hiddenItem = ReviewItem(product); var otherItem = ReviewItem(otherProduct);
            var reviewTime = new DateTimeOffset(2026, 11, 23, 3, 4, 0, TimeSpan.Zero);
            db.AddRange(category, product, otherProduct, lowItem, highItem, hiddenItem, otherItem,
                new ProductReview { ProductId = product.Id, StoreId = shop.Id, UserId = user.Id, OrderItemId = lowItem.Id, Rating = 1, Content = "Low review", Status = 1, CreatedAt = reviewTime, ReplyContent = "Store response", RepliedAt = reviewTime.AddMinutes(4) },
                new ProductReview { ProductId = product.Id, StoreId = shop.Id, UserId = user.Id, OrderItemId = highItem.Id, Rating = 5, Content = "High review", Status = 1, CreatedAt = reviewTime.AddMinutes(7) },
                new ProductReview { ProductId = product.Id, StoreId = shop.Id, UserId = user.Id, OrderItemId = hiddenItem.Id, Rating = 2, Content = "Hidden review", Status = 0, CreatedAt = reviewTime.AddMinutes(8) },
                new ProductReview { ProductId = otherProduct.Id, StoreId = active.Id, UserId = user.Id, OrderItemId = otherItem.Id, Rating = 3, Content = "Other store review", Status = 1, CreatedAt = reviewTime.AddMinutes(9) });
            product.SalePrice = 9000;
            product.Description = new string('x', 600);
            otherProduct.Status = 0;
            db.AddRange(
                new ProductImage { ProductId = product.Id, ImageUrl = "https://example.test/secondary.jpg", SortOrder = 0 },
                new ProductImage { ProductId = product.Id, ImageUrl = "https://example.test/primary.jpg", IsPrimary = true, SortOrder = 5 },
                new ProductVariant { ProductId = product.Id, SKU = "STORES-A", VariantName = "Active", Price = 10000, StockQuantity = 20, ReservedQuantity = 3, IsActive = true },
                new ProductVariant { ProductId = product.Id, SKU = "STORES-B", VariantName = "Hidden", Price = 10000, StockQuantity = 5, ReservedQuantity = 1, IsActive = false });
            db.AddRange(new RefreshToken { AccountId = owner.Id, TokenHash = "stores-session-one", FamilyId = Guid.NewGuid(), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) },
                new RefreshToken { AccountId = owner.Id, TokenHash = "stores-session-two", FamilyId = Guid.NewGuid(), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var storage = new FakeDocuments(); var service = new AdminStoreService(new CommerceStore(db), storage);
            var summary = await service.SummaryAsync(admin.Id, default);
            Assert.Equal(2, summary.Total); Assert.Equal(1, summary.Pending); Assert.Equal(1, summary.Active); Assert.Equal(0, summary.Inactive);
            var list = await service.ListAsync(admin.Id, new(Search: "owner search", Status: "pending"), default);
            Assert.Equal(shop.Id, Assert.Single(list.Items).Id);
            Assert.Empty((await service.ListAsync(admin.Id, new(Verification: "verified", Joined: "week"), default)).Items);
            Assert.Single((await service.ListAsync(admin.Id, new(Status: "active", Verification: "verified"), default)).Items);
            Assert.Empty((await service.ListAsync(admin.Id, new(Page: 10), default)).Items);
            var detail = await service.DetailAsync(admin.Id, shop.Id, 7, default);
            Assert.Equal(40000, detail.Stats.Sales); Assert.Equal(1, detail.Stats.Orders); Assert.False(detail.Store.Active);
            Assert.Equal(40000, Assert.Single(detail.Orders).Value);
            var orderDate = CommerceRules.VietnamDate(order.CreatedAt);
            var iso = (DateOnly day) => day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var byDate = await service.DetailAsync(admin.Id, shop.Id, 7, default, new(OrdersFrom: iso(orderDate), OrdersTo: iso(orderDate)));
            Assert.Equal(order.Id, Assert.Single(byDate.Orders).Id);
            var emptyDate = await service.DetailAsync(admin.Id, shop.Id, 7, default, new(OrdersPage: 100, OrdersFrom: iso(orderDate.AddDays(-1)), OrdersTo: iso(orderDate.AddDays(-1))));
            Assert.Empty(emptyDate.Orders);
            Assert.Equal(0, emptyDate.Pagination.Orders.TotalItems);
            Assert.Equal(1, emptyDate.Pagination.Orders.Page);
            Assert.Equal(1, emptyDate.Stats.Orders);
            var today = CommerceRules.VietnamDate(DateTimeOffset.UtcNow);
            var productsQuery = new AdminProductQuery { From = iso(today.AddDays(-29)), To = iso(today), Search = "Reviewed product", StoreId = shop.Id, MinPrice = 9000, MaxPrice = 9000, Stock = "available", Sort = "sold" };
            var productList = await service.ProductsAsync(admin.Id, productsQuery, default);
            var monitoredProduct = Assert.Single(productList.Items);
            Assert.Equal(product.Id, monitoredProduct.Id);
            Assert.Equal(17, monitoredProduct.AvailableStock);
            Assert.Equal(9000, monitoredProduct.Price);
            Assert.Equal(3, monitoredProduct.Rating);
            Assert.Equal(0, monitoredProduct.Sold);
            Assert.Equal(0, monitoredProduct.Revenue);
            Assert.DoesNotContain(productList.Stores, s => s.Id == hidden.Id || s.Id == brandStore.Id);
            var completedOrder = await db.Set<Order>().SingleAsync(o => o.Id == order.Id);
            completedOrder.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            var withSales = await service.ProductsAsync(admin.Id, productsQuery, default);
            Assert.Equal(3, Assert.Single(withSales.Items).Sold);
            Assert.Equal(30000, Assert.Single(withSales.Items).Revenue);
            Assert.Equal(4, withSales.Summary.Sold);
            completedOrder.PaymentStatus = "REFUNDED";
            await db.SaveChangesAsync();
            Assert.Equal(0, (await service.ProductsAsync(admin.Id, productsQuery, default)).Summary.Sold);
            completedOrder.PaymentStatus = "PAID";
            completedOrder.CompletedAt = null;
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            productsQuery.Stock = "out";
            Assert.Empty((await service.ProductsAsync(admin.Id, productsQuery, default)).Items);
            await Assert.ThrowsAsync<BusinessException>(() => service.ProductsAsync(owner.Id, productsQuery, default));
            foreach (var range in new[] {
                new AdminStoreDetailQuery(OrdersFrom: iso(today)),
                new AdminStoreDetailQuery(OrdersTo: iso(today)),
                new AdminStoreDetailQuery(OrdersFrom: "2026-02-30", OrdersTo: iso(today)),
                new AdminStoreDetailQuery(OrdersFrom: iso(today), OrdersTo: iso(today.AddDays(-1))),
                new AdminStoreDetailQuery(OrdersFrom: iso(today), OrdersTo: iso(today.AddDays(1))),
                new AdminStoreDetailQuery(OrdersFrom: iso(today.AddDays(-93)), OrdersTo: iso(today)),
                new AdminStoreDetailQuery(OrdersFrom: "1999-12-31", OrdersTo: "2000-01-01") })
                await Assert.ThrowsAsync<BusinessException>(() => service.DetailAsync(admin.Id, shop.Id, 7, default, range));
            await service.DetailAsync(admin.Id, shop.Id, 7, default, new(OrdersFrom: iso(today.AddDays(-92)), OrdersTo: iso(today)));
            var orderCard = Assert.Single(detail.OrderDetails);
            Assert.Equal(order.Id, orderCard.Id);
            Assert.Equal("Recipient Test", orderCard.RecipientName);
            Assert.Equal("0912345678", orderCard.RecipientPhone);
            Assert.Equal(40000, orderCard.TotalAmount);
            Assert.Equal("SHIPPING", orderCard.Status);
            Assert.Equal("PAID", orderCard.PaymentStatus);
            Assert.Equal("COD", orderCard.PaymentMethod);
            Assert.Equal(3, orderCard.ItemCount);
            Assert.Equal(3, orderCard.Quantity);
            Assert.NotNull(orderCard.FirstItem);
            Assert.Equal(product.Id, orderCard.FirstItem.ProductId);
            Assert.Equal(product.Name, orderCard.FirstItem.Name);
            Assert.Equal("https://example.test/primary.jpg", orderCard.FirstItem.ImageUrl);
            Assert.Equal(detail.Orders.Select(o => o.Id), detail.OrderDetails.Select(o => o.Id));
            Assert.Equal("https://example.test/primary.jpg", orderCard.FirstItem.ImageUrl);
            Assert.Equal(2, detail.Stats.Reviews); Assert.Equal(2, detail.ReviewDetails.Count);
            var productCard = Assert.Single(detail.ProductDetails);
            Assert.Equal(product.Id, productCard.Id);
            Assert.Equal("https://example.test/primary.jpg", productCard.ImageUrl);
            Assert.Equal(product.SKU, productCard.SKU);
            Assert.Equal(category.Name, productCard.CategoryName);
            Assert.Equal(brand.Name, productCard.BrandName);
            Assert.Equal(10000, productCard.BasePrice);
            Assert.Equal(9000, productCard.SalePrice);
            Assert.Equal(25, productCard.StockQuantity);
            Assert.Equal(4, productCard.ReservedQuantity);
            Assert.Equal(17, productCard.AvailableStock);
            Assert.Equal(2, productCard.VariantCount);
            Assert.Equal(500, productCard.Description.Length);
            Assert.False(productCard.IsDeleted);
            var fullProduct = await service.ProductAsync(admin.Id, shop.Id, product.Id, default);
            Assert.Equal(product.Id, fullProduct.Product.Id);
            Assert.Equal(600, fullProduct.Description.Length);
            Assert.Equal(2, fullProduct.Images.Count);
            Assert.Equal("https://example.test/primary.jpg", fullProduct.Images[0]);
            Assert.Equal(2, fullProduct.Variants.Count);
            Assert.Contains(fullProduct.Variants, v => !v.Active && v.Stock == 5);
            Assert.Equal(shop.Id, fullProduct.Store.Id);
            Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => service.ProductAsync(admin.Id, active.Id, product.Id, default))).StatusCode);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.ProductAsync(owner.Id, shop.Id, product.Id, default))).StatusCode);
            Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => service.ProductAsync(admin.Id, shop.Id, Guid.NewGuid(), default))).StatusCode);
            var fullOrder = await service.OrderAsync(admin.Id, shop.Id, order.Id, default);
            Assert.Equal(shop.Id, fullOrder.Store.Id);
            Assert.Equal(40000, fullOrder.TotalAmount);
            Assert.Equal(3, fullOrder.Items.Count);
            Assert.All(fullOrder.Items, item => Assert.Equal(product.Id, item.ProductId));
            Assert.Equal("Recipient Test", fullOrder.RecipientName);
            Assert.Equal("COD", fullOrder.PaymentMethod);
            Assert.Equal("SHIPPING", fullOrder.Status);
            var secondBrand = new Brand { OwnerAccountId = owner.Id, Name = "Second brand", Slug = "admin-stores-second-brand", Status = 1 };
            var secondSeller = new SellerOrder { StoreId = shop.Id, BrandId = secondBrand.Id, OrderId = order.Id, Status = "SHIPPING", Subtotal = 5000, TotalAmount = 5000, CreatedAt = order.CreatedAt };
            db.AddRange(secondBrand, secondSeller);
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var multiBrand = await service.OrderAsync(admin.Id, shop.Id, order.Id, default);
            Assert.Equal(45000, multiBrand.TotalAmount);
            Assert.Equal(3, multiBrand.Items.Count);
            var multiBrandList = await service.DetailAsync(admin.Id, shop.Id, 7, default);
            Assert.Equal(1, multiBrandList.Stats.Orders);
            Assert.Equal(45000, Assert.Single(multiBrandList.OrderDetails).TotalAmount);
            db.Remove(secondSeller); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => service.OrderAsync(admin.Id, active.Id, order.Id, default))).StatusCode);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.OrderAsync(owner.Id, shop.Id, order.Id, default))).StatusCode);
            Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => service.OrderAsync(admin.Id, shop.Id, Guid.NewGuid(), default))).StatusCode);
            Assert.Equal(detail.Products.Select(p => p.Id), detail.ProductDetails.Select(p => p.Id));
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.DetailAsync(owner.Id, shop.Id, 7, default))).StatusCode);
            var secondPage = await service.DetailAsync(admin.Id, shop.Id, 7, default, new(ProductsPage: 2, OrdersPage: 2, ReviewsPage: 2, HistoryPage: 2, PageSize: 1));
            Assert.Equal("Low review", Assert.Single(secondPage.ReviewDetails).Content);
            Assert.Equal(2, secondPage.Pagination.Reviews.TotalItems);
            Assert.Equal(2, secondPage.Pagination.Reviews.Page);
            Assert.Equal(1, secondPage.Pagination.Products.Page);
            Assert.Equal(1, secondPage.Pagination.Orders.Page);
            Assert.Empty(secondPage.History);
            Assert.Equal(0, secondPage.Pagination.History.TotalItems);
            await Assert.ThrowsAsync<BusinessException>(() => service.DetailAsync(admin.Id, shop.Id, 7, default, new(ReviewsPage: 0)));
            await Assert.ThrowsAsync<BusinessException>(() => service.DetailAsync(admin.Id, shop.Id, 7, default, new(PageSize: 21)));
            Assert.Equal(new short[] { 5, 1 }, detail.ReviewDetails.Select(r => r.Rating).ToArray());
            Assert.All(detail.ReviewDetails, r => Assert.Equal("Kai", r.DisplayName));
            var answered = detail.ReviewDetails.Single(r => r.Rating == 1);
            Assert.Equal("Store response", answered.Reply!.Content); Assert.Equal(shop.Name, answered.Reply.StoreName);
            Assert.Equal(reviewTime.AddMinutes(4), answered.Reply.UpdatedAt);
            Assert.Null(detail.ReviewDetails.Single(r => r.Rating == 5).Reply);
            Assert.Equal(2, detail.Reviews.Count); // Legacy response remains available.
            using (var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(detail, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))))
                Assert.Equal("Kai", json.RootElement.GetProperty("reviewDetails")[0].GetProperty("displayName").GetString());
            var doc = await service.DocumentAsync(admin.Id, shop.Id, verification.Id, "license", default);
            Assert.Equal("AQID", doc.Content); Assert.Equal(1, storage.Reads);
            Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => service.DocumentAsync(admin.Id, active.Id, verification.Id, "license", default))).StatusCode);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.SummaryAsync(owner.Id, default))).StatusCode);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.DocumentAsync(owner.Id, shop.Id, verification.Id, "license", default))).StatusCode);
            await Assert.ThrowsAsync<BusinessException>(() => service.ListAsync(admin.Id, new(PageSize: 500), default));
            await Assert.ThrowsAsync<BusinessException>(() => service.StatusAsync(admin.Id, shop.Id, new(true, "", detail.Store.UpdatedAt), default));
            await Assert.ThrowsAsync<BusinessException>(() => service.ReviewAsync(admin.Id, shop.Id, new(verification.Id, false, "", detail.Store.UpdatedAt), default));
            var otherDetail = await service.DetailAsync(admin.Id, active.Id, 7, default);
            Assert.Empty(otherDetail.OrderDetails);
            Assert.False(Assert.Single(otherDetail.ProductDetails).IsDeleted);
            Assert.Equal(0, otherDetail.ProductDetails[0].Status);
            Assert.Equal("", otherDetail.ProductDetails[0].ImageUrl);
            Assert.Equal(0, otherDetail.ProductDetails[0].AvailableStock);
            Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => service.ReviewAsync(admin.Id, active.Id, new(verification.Id, true, "", otherDetail.Store.UpdatedAt), default))).StatusCode);
            db.ChangeTracker.Clear();
            await service.ReviewAsync(admin.Id, shop.Id, new(verification.Id, true, "", detail.Store.UpdatedAt), default);
            db.ChangeTracker.Clear();
            detail = await service.DetailAsync(admin.Id, shop.Id, 7, default);
            Assert.True(detail.Store.IsVerified); Assert.False(detail.Store.Active); Assert.Equal("inactive", detail.Store.Status);
            var stale = detail.Store.UpdatedAt;
            await service.StatusAsync(admin.Id, shop.Id, new(true, "Reopen after review", stale), default);
            Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => service.StatusAsync(admin.Id, shop.Id, new(false, "Stale update", stale), default))).StatusCode);
            db.ChangeTracker.Clear();
            detail = await service.DetailAsync(admin.Id, shop.Id, 7, default);
            Assert.True(detail.Store.Active); Assert.Contains(detail.History, h => h.Title == "ACTIVATE_STORE" && h.Subtitle == "Reopen after review");
            await service.StatusAsync(admin.Id, shop.Id, new(false, "Pause selling only", detail.Store.UpdatedAt), default); db.ChangeTracker.Clear();
            detail = await service.DetailAsync(admin.Id, shop.Id, 7, default);
            Assert.False(detail.Store.Active); Assert.False(detail.Store.OwnerSuspended); Assert.True(detail.Store.CanSuspendOwner);
            Assert.Equal(1, (await db.Accounts.SingleAsync(a => a.Id == owner.Id)).Status);
            Assert.All(await db.RefreshTokens.Where(t => t.AccountId == owner.Id).ToListAsync(), t => Assert.Null(t.RevokedAt));
            Assert.Contains(await new SellerService(new CommerceStore(db)).StoresAsync(owner.Id, default), s => s.Id == shop.Id);
            var created = await service.SaveAsync(admin.Id, null, new("Internal store", "internal-store", "", "", "Description"), default);
            Assert.Equal(admin.Id, (await db.Stores.SingleAsync(s => s.Id == created)).OwnerAccountId);
            var internalDetail = await service.DetailAsync(admin.Id, created, 7, default);
            Assert.False(internalDetail.Store.CanSuspendOwner);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.SuspendOwnerAsync(admin.Id, created, new("Protected owner", internalDetail.Store.UpdatedAt), default))).StatusCode);
            Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(admin.Id, null, new("Duplicate", "internal-store", "", "", ""), default))).StatusCode);
            Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => service.DetailAsync(admin.Id, brandStore.Id, 7, default))).StatusCode);
            await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(admin.Id, null, new("Invalid", "invalid-store", "invalid email", "", ""), default));
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.SuspendOwnerAsync(owner.Id, shop.Id, new("Not admin", detail.Store.UpdatedAt), default))).StatusCode);
            Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => service.SuspendOwnerAsync(admin.Id, shop.Id, new("Stale ban", stale), default))).StatusCode);
            db.ChangeTracker.Clear(); Assert.Equal(1, (await db.Accounts.SingleAsync(a => a.Id == owner.Id)).Status);
            await service.SuspendOwnerAsync(admin.Id, shop.Id, new("Suspend owner account", detail.Store.UpdatedAt), default); db.ChangeTracker.Clear();
            Assert.Equal(0, (await db.Accounts.SingleAsync(a => a.Id == owner.Id)).Status);
            Assert.All(await db.RefreshTokens.Where(t => t.AccountId == owner.Id).ToListAsync(), t => Assert.NotNull(t.RevokedAt));
            Assert.All(await db.Stores.Where(s => s.OwnerAccountId == owner.Id && s.StoreType == 1).ToListAsync(), s => Assert.Equal(0, s.Status));
            Assert.Empty(await new SellerService(new CommerceStore(db)).StoresAsync(owner.Id, default));
            detail = await service.DetailAsync(admin.Id, shop.Id, 7, default);
            Assert.True(detail.Store.OwnerSuspended); Assert.False(detail.Store.CanSuspendOwner);
            Assert.Contains(detail.History, h => h.Title == "SUSPEND_STORE_OWNER");
            Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => service.StatusAsync(admin.Id, shop.Id, new(true, "Cannot reopen banned owner", detail.Store.UpdatedAt), default))).StatusCode);
            // More than the old 20-item cap, including equal timestamps to exercise tie ordering.
            for (var i = 0; i < 23; i++)
            {
                var extraProduct = new Product { Name = $"Page product {i}", Slug = $"page-product-{i}", SKU = $"PAGE-{i}", CategoryId = category.Id, BrandId = brand.Id, StoreId = active.Id, BasePrice = 10000, Status = 1, CreatedAt = order.CreatedAt };
                var extraOrder = new Order { UserId = user.Id, OrderCode = $"PAGE-{i}", Status = "COMPLETED", PaymentStatus = "PAID", Subtotal = 10000, TotalAmount = 10000, IdempotencyKey = $"page-order-{i}", CreatedAt = order.CreatedAt };
                var item = new OrderItem { OrderId = extraOrder.Id, ProductId = extraProduct.Id, BrandId = brand.Id, StoreId = active.Id, ProductName = extraProduct.Name, Quantity = 1, UnitPrice = 10000, TotalPrice = 10000 };
                db.AddRange(extraProduct, extraOrder, item,
                    new SellerOrder { StoreId = active.Id, BrandId = brand.Id, OrderId = extraOrder.Id, TotalAmount = 10000, CreatedAt = order.CreatedAt },
                    new ProductReview { ProductId = extraProduct.Id, StoreId = active.Id, UserId = user.Id, OrderItemId = item.Id, Rating = 5, Content = $"Page review {i}", Status = 1, CreatedAt = reviewTime },
                    new AuditLog { AccountId = admin.Id, EntityType = "Store", EntityId = active.Id, Action = "UPDATE_STORE", Details = $"Page history {i}", CreatedAt = reviewTime });
            }
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var pages = new List<AdminStoreDetail>();
            for (var page = 1; page <= 3; page++)
                pages.Add(await service.DetailAsync(admin.Id, active.Id, 7, default, new(page, page, page, page)));
            Assert.Equal(24, pages.SelectMany(p => p.Products).Select(p => p.Id).Distinct().Count());
            Assert.Equal(23, pages.SelectMany(p => p.Orders).Select(p => p.Id).Distinct().Count());
            Assert.Equal(24, pages.SelectMany(p => p.ReviewDetails).Select(p => p.Id).Distinct().Count());
            Assert.Equal(24, pages.SelectMany(p => p.History).Select(p => p.Id).Distinct().Count());
            Assert.Equal(4, pages[2].Products.Count);
            Assert.All(pages, page => Assert.Equal(page.Products.Select(p => p.Id), page.ProductDetails.Select(p => p.Id)));
            Assert.All(pages, page => Assert.Equal(page.Orders.Select(o => o.Id), page.OrderDetails.Select(o => o.Id)));
            Assert.Equal(3, pages[2].Orders.Count);
            Assert.Equal(24, pages[2].Pagination.Products.TotalItems);
            Assert.Equal(24, pages[2].Pagination.History.TotalItems);
            var beyond = await service.DetailAsync(admin.Id, active.Id, 7, default, new(ProductsPage: 100000));
            Assert.Equal(3, beyond.Pagination.Products.Page);
            Assert.Equal(pages[2].Products.Select(p => p.Id), beyond.Products.Select(p => p.Id));
            var boundaryDay = today.AddDays(-2);
            var boundary = new DateTimeOffset(boundaryDay.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
            var boundaryOrders = new List<Order>();
            foreach (var moment in new[] { boundary.AddTicks(-10), boundary, boundary.AddDays(1) })
            {
                var boundaryOrder = new Order { UserId = user.Id, OrderCode = $"PERIOD-{boundaryOrders.Count}", IdempotencyKey = $"period-{boundaryOrders.Count}", Status = "COMPLETED", PaymentStatus = "PAID", Subtotal = 50, TotalAmount = 50, CreatedAt = moment };
                boundaryOrders.Add(boundaryOrder);
                db.AddRange(boundaryOrder, new SellerOrder { StoreId = active.Id, BrandId = brand.Id, OrderId = boundaryOrder.Id, TotalAmount = 50, CreatedAt = moment });
            }
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var midnightPeriod = await service.DetailAsync(admin.Id, active.Id, 7, default, new(OrdersFrom: iso(boundaryDay), OrdersTo: iso(boundaryDay)));
            Assert.Equal(boundaryOrders[1].Id, Assert.Single(midnightPeriod.Orders).Id);
            Assert.Equal(1, midnightPeriod.Pagination.Orders.TotalItems);
            Assert.All(midnightPeriod.OrderDetails, row => Assert.Equal(boundaryOrders[1].Id, row.Id));
            (await db.Set<Account>().SingleAsync(a => a.Id == admin.Id)).Status = 0; await db.SaveChangesAsync();
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.StatusAsync(admin.Id, shop.Id, new(false, "Denied", detail.Store.UpdatedAt), default))).StatusCode);
        }
        finally { NpgsqlConnection.ClearAllPools(); await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{scratch.Database}\" WITH (FORCE)", connection); await drop.ExecuteNonQueryAsync(); }
    }
    private sealed class FakeDocuments : IVerificationDocumentStorage
    {
        public int Reads;
        public Task UploadAsync(string publicId, VerificationFileInput file, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteAsync(string publicId, CancellationToken ct) => Task.CompletedTask;
        public Task<byte[]> ReadAsync(VerificationDocument document, CancellationToken ct) { Reads++; return Task.FromResult(new byte[] { 1, 2, 3 }); }
    }
}
