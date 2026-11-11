using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace BCOBCA.Tests;

public sealed class PostgresFlowTests
{
    [Fact]
    public async Task CoreUserAndCommerceFlowPreservesAuthorizationAndAtomicity()
    {
        EnvironmentBootstrap.Load();
        var adminConnection = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Set a PostgreSQL development connection to run integration tests.");
        var databaseName = "bcobca_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName }.ConnectionString;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.MigrateAsync();
            var categoryColumns = await db.Database.SqlQueryRaw<string>("SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Categories'").ToListAsync();
            Assert.DoesNotContain("IconUrl", categoryColumns);
            Assert.Contains("ImageUrl", categoryColumns);
            Assert.Contains("AttributeDefinitions", categoryColumns);
            Assert.Equal(8, categoryColumns.Count);
            Assert.Equal(68, db.Model.GetEntityTypes().Count());
            var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var store = new CommerceStore(db);
            var catalog = new CatalogService(store);
            Assert.Empty((await catalog.ProductsAsync(new CatalogQuery(), default)).Items);
            Assert.Empty((await catalog.ServicesAsync(new CatalogQuery(), default)).Items);
            var auth = new AuthService(store, config, new RegisterValidator(), new LoginValidator(), new ProfileValidator());
            var created = await auth.RegisterAsync(new RegisterRequest("test@example.com", "StrongPassword123!", "Khách kiểm thử"), default);
            Assert.Equal(new[] { "Customer" }, created.User.Roles);
            Assert.DoesNotContain("StrongPassword", (await db.Accounts.SingleAsync()).PasswordHash);
            var memberService = new MemberService(store);
            var program = await memberService.CheckInProgramAsync(default);
            Assert.True(program.IsActive);
            Assert.Equal(1, program.RewardPoints);
            var beforeCheckIn = await memberService.MembershipAsync(created.User.Id, default);
            Assert.Equal(0, beforeCheckIn.CheckInStreakDays);
            Assert.False(beforeCheckIn.CheckedInToday);
            var checkIn = await memberService.CheckInAsync(created.User.Id, default);
            Assert.Equal(1, checkIn.RewardPoints);
            var afterCheckIn = await memberService.MembershipAsync(created.User.Id, default);
            Assert.Equal(1, afterCheckIn.CheckInStreakDays);
            Assert.True(afterCheckIn.CheckedInToday);
            var savedCheckIn = await db.DailyCheckIns.SingleAsync(x => x.UserId == created.User.Id);
            savedCheckIn.CheckInDate = checkIn.Date.AddDays(-1); savedCheckIn.StreakDays = 6;
            await db.SaveChangesAsync();
            var continuing = await memberService.MembershipAsync(created.User.Id, default);
            Assert.Equal(6, continuing.CheckInStreakDays); Assert.False(continuing.CheckedInToday);
            savedCheckIn.CheckInDate = checkIn.Date.AddDays(-2);
            await db.SaveChangesAsync();
            Assert.Equal(0, (await memberService.MembershipAsync(created.User.Id, default)).CheckInStreakDays);
            savedCheckIn.CheckInDate = checkIn.Date; savedCheckIn.StreakDays = 1;
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BCOBCA.BLL.Common.BusinessException>(() => memberService.CheckInAsync(created.User.Id, default));
            Assert.Equal(1, await db.DailyCheckIns.CountAsync());
            Assert.Equal(1, (await db.UserMemberships.SingleAsync()).AvailablePoints);
            Assert.Equal(1, await db.PointTransactions.CountAsync());
            var category = new Category { Name = "Test category", Slug = "test-category", IsActive = true };
            db.Categories.Add(category);
            var now = DateTimeOffset.UtcNow;
            var variantIds = new List<Guid>();
            for (var index = 0; index < 2; index++)
            {
                var brand = new Brand { OwnerAccountId = created.User.AccountId, Name = "Test brand " + index, Slug = "test-brand-" + index, Status = 1, CreatedAt = now, UpdatedAt = now };
                var product = new Product { StoreId = TestStores.Own(db, brand), BrandId = brand.Id, CategoryId = category.Id, Name = "Test product " + index, Slug = "test-product-" + index, SKU = "TEST-" + index, BasePrice = 100000, Status = 1, CreatedAt = now, UpdatedAt = now };
                var variant = new ProductVariant { ProductId = product.Id, SKU = "TEST-V-" + index, VariantName = "Default", Price = 100000, StockQuantity = 2, IsActive = true };
                db.Brands.Add(brand); db.Products.Add(product); db.ProductVariants.Add(variant); variantIds.Add(variant.Id);
            }
            await db.SaveChangesAsync();
            var address = await memberService.SaveAddressAsync(created.User.Id, null, new AddressRequest("Khách kiểm thử", "0901234567", "79", "001", "Địa chỉ chỉ dùng kiểm thử", true), default);
            var orderService = TestOrders.Create(store, config, new FinanceService(store, config));
            foreach (var variantId in variantIds)
            {
                var variant = await db.ProductVariants.SingleAsync(x => x.Id == variantId);
                await orderService.AddAsync(created.User.Id, new CartItemRequest(variant.ProductId, variantId, 1), default);
            }
            var checkout = new CheckoutRequest(address.Id);
            var voucher = new Voucher { Code = "TEST10", Name = "Test-only voucher", DiscountType = "PERCENT", DiscountValue = 10, MinOrderAmount = 1000, PerUserLimit = 1, StartsAt = now.AddMinutes(-1), ExpiresAt = now.AddDays(1), Status = "ACTIVE" };
            db.Vouchers.Add(voucher);
            db.VoucherTargets.Add(new VoucherTarget { VoucherId = voucher.Id, BrandId = (await db.Brands.FirstAsync()).Id });
            await db.SaveChangesAsync();
            var quote = await orderService.PreviewAsync(created.User.Id, new CheckoutPreviewRequest("TEST10"), default);
            Assert.Equal(10000, quote.DiscountAmount);
            Assert.Equal(0, await db.VoucherUsages.CountAsync());
            Assert.All(await db.ProductVariants.ToListAsync(), x => Assert.Equal(0, x.ReservedQuantity));
            checkout = checkout with { VoucherCode = "TEST10", ExpectedTotal = quote.TotalAmount, ExpectedCartHash = quote.CartHash };
            await using (var mismatchDb = new ApplicationDbContext(options))
            {
                var mismatchStore = new CommerceStore(mismatchDb);
                var mismatchService = TestOrders.Create(mismatchStore, config, new FinanceService(mismatchStore, config));
                await Assert.ThrowsAsync<BCOBCA.BLL.Common.BusinessException>(() => mismatchService.CheckoutAsync(created.User.Id, "test-mismatch", checkout with { ExpectedTotal = 1 }, default));
            }
            await using (var changedCartDb = new ApplicationDbContext(options))
            {
                var changedStore = new CommerceStore(changedCartDb);
                var changedOrders = TestOrders.Create(changedStore, config, new FinanceService(changedStore, config));
                await Assert.ThrowsAsync<BCOBCA.BLL.Common.BusinessException>(() => changedOrders.CheckoutAsync(created.User.Id, "test-cart-mismatch", checkout with { ExpectedCartHash = new string('0', 64) }, default));
            }
            Assert.Equal(0, await db.Orders.CountAsync());
            var order = await orderService.CheckoutAsync(created.User.Id, "test-idempotency", checkout, default);
            Assert.Equal(190000 + config.GetValue<decimal>("Checkout:ShippingFee"), order.CheckoutOrders!.Sum(x => x.TotalAmount));
            Assert.Equal(2, await db.SellerOrders.CountAsync());
            Assert.Equal(2, await db.InventoryReservations.CountAsync());
            Assert.All(await db.ProductVariants.ToListAsync(), x => Assert.Equal(1, x.ReservedQuantity));
            Assert.Empty((await orderService.CartAsync(created.User.Id, default)).Items);
            Assert.Equal(order.Id, (await orderService.CheckoutAsync(created.User.Id, "test-idempotency", checkout, default)).Id);
            Assert.Equal(2, await db.Orders.CountAsync());
            Assert.Equal(order.Id, (await orderService.FindRequestAsync(created.User.Id, "test-idempotency", default))!.Id);
            Assert.Null(await orderService.FindRequestAsync(Guid.NewGuid(), "test-idempotency", default));
            Assert.Equal("USED", (await db.UserVouchers.SingleAsync()).Status);
            Assert.DoesNotContain((await memberService.VouchersAsync(created.User.Id, new CatalogQuery(), default)).Items, x => x.Id == voucher.Id);
            await Assert.ThrowsAsync<BCOBCA.BLL.Common.BusinessException>(() => orderService.CheckoutAsync(created.User.Id, "test-idempotency", checkout with { Note = "Changed" }, default));
            await Assert.ThrowsAsync<BCOBCA.BLL.Common.BusinessException>(() => orderService.OrderAsync(Guid.NewGuid(), order.Id, default));
            foreach (var child in order.CheckoutOrders!) await orderService.CancelAsync(created.User.Id, child.Id, default);
            Assert.All(await db.ProductVariants.ToListAsync(), x => Assert.Equal(0, x.ReservedQuantity));
            Assert.All(await db.InventoryReservations.ToListAsync(), x => Assert.Equal("RELEASED", x.Status));
            Assert.Equal("CANCELLED", (await orderService.OrderAsync(created.User.Id, order.Id, default)).Status);
            Assert.Equal("AVAILABLE", (await db.UserVouchers.SingleAsync()).Status);
            Assert.Contains((await memberService.VouchersAsync(created.User.Id, new CatalogQuery(), default)).Items, x => x.Id == voucher.Id);
            var firstVariant = await db.ProductVariants.FirstAsync();
            await orderService.AddAsync(created.User.Id, new CartItemRequest(firstVariant.ProductId, firstVariant.Id, 1), default);
            var abandoned = await orderService.CheckoutAsync(created.User.Id, "test-expiry", checkout with { VoucherCode = null, ExpectedTotal = null, ExpectedCartHash = null }, default);
            var hold = await db.InventoryReservations.SingleAsync(x => x.OrderId == abandoned.Id);
            hold.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
            Assert.Equal(1, await orderService.ExpireOneReservationAsync(default));
            Assert.Equal("CANCELLED", (await orderService.OrderAsync(created.User.Id, abandoned.Id, default)).Status);
            Assert.Equal(0, (await db.ProductVariants.SingleAsync(x => x.Id == firstVariant.Id)).ReservedQuantity);
            Assert.Equal(0, await orderService.ExpireOneReservationAsync(default));
            var firstSession = await auth.LoginAsync(new LoginRequest("test@example.com", "StrongPassword123!"), default);
            var refreshed = await auth.RefreshAsync(firstSession.RefreshToken, default);
            Assert.NotEqual(firstSession.RefreshToken, refreshed.RefreshToken);
            await Assert.ThrowsAsync<BCOBCA.BLL.Common.BusinessException>(() => auth.RefreshAsync(firstSession.RefreshToken, default));
            await Assert.ThrowsAsync<BCOBCA.BLL.Common.BusinessException>(() => auth.RefreshAsync(refreshed.RefreshToken, default));
            var security = new AccountSecurityService(store, config);
            var resetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            db.AccountActionTokens.Add(new AccountActionToken { AccountId = created.User.AccountId, TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resetToken))), Action = "RESET_PASSWORD", CreatedAt = now, ExpiresAt = now.AddMinutes(5) });
            await db.SaveChangesAsync();
            await security.ResetAsync(new ResetPasswordRequest(resetToken, "NewStrongPassword456!"), default);
            await Assert.ThrowsAsync<BCOBCA.BLL.Common.BusinessException>(() => security.ResetAsync(new ResetPasswordRequest(resetToken, "AnotherPassword789!"), default));
            await Assert.ThrowsAsync<BCOBCA.BLL.Common.BusinessException>(() => auth.LoginAsync(new LoginRequest("test@example.com", "StrongPassword123!"), default));
            var newSession = await auth.LoginAsync(new LoginRequest("test@example.com", "NewStrongPassword456!"), default);
            Assert.NotEmpty(await security.SessionsAsync(created.User.AccountId, default));
            await auth.LogoutAsync(newSession.RefreshToken, default);
            Assert.Empty(await security.SessionsAsync(created.User.AccountId, default));
            var second = await auth.RegisterAsync(new RegisterRequest("second@example.com", "StrongPassword123!", "Khách kiểm thử hai"), default);
            async Task<bool> AttemptCheckIn()
            {
                await using var concurrent = new ApplicationDbContext(options);
                try { await new MemberService(new CommerceStore(concurrent)).CheckInAsync(second.User.Id, default); return true; }
                catch (BCOBCA.BLL.Common.BusinessException ex) when (ex.StatusCode == 409) { return false; }
                catch (Exception ex) when (BCOBCA.BLL.Common.DatabaseConflicts.IsConcurrency(ex)) { return false; }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" }) { return false; }
            }
            var races = await Task.WhenAll(AttemptCheckIn(), AttemptCheckIn());
            Assert.Single(races, success => success);
            Assert.Equal(1, await db.DailyCheckIns.CountAsync(x => x.UserId == second.User.Id));
            db.ChangeTracker.Clear();
            Assert.Equal(1, (await db.UserMemberships.SingleAsync(x => x.UserId == second.User.Id)).AvailablePoints);
            var adminCatalog = new AdminCatalogService(store);
            var forbidden = await Assert.ThrowsAsync<BCOBCA.BLL.Common.BusinessException>(() => adminCatalog.SaveBrandAsync(created.User.AccountId, null, new BrandInput("Test owned brand", "test-owned-brand"), default));
            Assert.Equal(403, forbidden.StatusCode);
            db.AccountRoles.Add(new AccountRole { AccountId = created.User.AccountId, Role = "BrandOwner" });
            await db.SaveChangesAsync();
            var ownedBrandId = await adminCatalog.SaveBrandAsync(created.User.AccountId, null, new BrandInput("Test owned brand", "test-owned-brand"), default);
            var adminProductInput = new ProductInput(ownedBrandId, category.Id, "Admin-created test product", "admin-created-test-product", "ADMIN-TEST", 1000,
                [new VariantInput(null, "ADMIN-TEST-V1", "Standard", 1000, 5)], [], StoreId: await db.Stores.Where(s => s.OwnerAccountId == created.User.AccountId).Select(s => s.Id).FirstAsync());
            var adminProductId = await adminCatalog.SaveProductAsync(created.User.AccountId, null, adminProductInput, default);
            var crossBrand = await Assert.ThrowsAsync<BCOBCA.BLL.Common.BusinessException>(() => adminCatalog.SaveProductAsync(second.User.AccountId, adminProductId, adminProductInput, default));
            Assert.Equal(403, crossBrand.StatusCode);
            var adminVariant = await db.ProductVariants.SingleAsync(x => x.ProductId == adminProductId);
            adminVariant.ReservedQuantity = 3; await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BCOBCA.BLL.Common.BusinessException>(() => adminCatalog.SaveProductAsync(created.User.AccountId, adminProductId, adminProductInput with { Variants = [new VariantInput(adminVariant.Id, adminVariant.SKU, adminVariant.VariantName, 1000, 2)] }, default));
            db.ChangeTracker.Clear();
            await orderService.AddAsync(created.User.Id, new CartItemRequest(adminProductId, adminVariant.Id, 1), default);
            await adminCatalog.ArchiveProductAsync(created.User.AccountId, adminProductId, default);
            var archivedCart = await orderService.CartAsync(created.User.Id, default);
            var archivedItem = Assert.Single(archivedCart.Items);
            Assert.False(archivedItem.IsAvailable);
            await orderService.RemoveAsync(created.User.Id, archivedItem.Id, default);
            Assert.Empty((await orderService.CartAsync(created.User.Id, default)).Items);
            Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == "ARCHIVE" && x.EntityId == adminProductId));
            db.AccountRoles.AddRange(new AccountRole { AccountId = created.User.AccountId, Role = "SuperAdmin" }, new AccountRole { AccountId = second.User.AccountId, Role = "SuperAdmin" });
            await db.SaveChangesAsync();
            var barrier = new SuperAdminReadBarrier();
            var raceOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).AddInterceptors(barrier).Options;
            async Task<bool> RevokePeer(Guid actor, Guid target)
            {
                await using var raceDb = new ApplicationDbContext(raceOptions);
                try { await new AdminCatalogService(new CommerceStore(raceDb)).SetRoleAsync(actor, new AccountRoleInput(target, "SuperAdmin", false), default); return true; }
                catch (BCOBCA.BLL.Common.BusinessException ex) when (ex.StatusCode is 403 or 409) { return false; }
                catch (Exception ex) when (BCOBCA.BLL.Common.DatabaseConflicts.IsConcurrency(ex)) { return false; }
            }
            var roleRace = await Task.WhenAll(RevokePeer(created.User.AccountId, second.User.AccountId), RevokePeer(second.User.AccountId, created.User.AccountId));
            Assert.Equal(2, barrier.Hits); Assert.Single(roleRace, success => success);
            Assert.Equal(1, await db.AccountRoles.CountAsync(x => x.Role == "SuperAdmin"));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class SuperAdminReadBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int hits;
        public int Hits => hits;
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            // Hold both successful authority reads before either peer can mutate roles.
            if (command.CommandText.Contains("SELECT EXISTS", StringComparison.Ordinal) && command.CommandText.Contains("AccountRoles", StringComparison.Ordinal) && command.CommandText.Contains("'SuperAdmin'", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref hits) == 2) released.TrySetResult();
                await released.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }
}
