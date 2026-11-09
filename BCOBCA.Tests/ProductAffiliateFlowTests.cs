using System.Security.Cryptography;
using System.Data.Common;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class ProductAffiliateFlowTests
{
    [Fact]
    public async Task ProductLinksFreezeSingleSourceAndRespectDiscountsCapsBudgetPayoutAndRefund()
    {
        EnvironmentBootstrap.Load();
        var databaseName = "bcobca_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__Default"));
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = databaseName }.ConnectionString;
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
            await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
            var store = new CommerceStore(db);
            var config = new ConfigurationBuilder().AddEnvironmentVariables().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:PublicUrl"] = "https://shop.example.test", ["Affiliate:AttributionDays"] = "30", ["Affiliate:ApprovalHoldDays"] = "0",
                ["Membership:SpendPerPoint"] = "10000", ["Checkout:ShippingFee"] = "20000", ["Settlement:PlatformFeeRate"] = "0.1",
                ["Security:BankDataKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            }).Build();
            var auth = new AuthService(store, config, new RegisterValidator(), new LoginValidator(), new ProfileValidator());
            var owner = await auth.RegisterAsync(new RegisterRequest("owner@example.com", "StrongPassword123!", "Owner"), default);
            var other = await auth.RegisterAsync(new RegisterRequest("other@example.com", "StrongPassword123!", "Other"), default);
            var buyer = await auth.RegisterAsync(new RegisterRequest("buyer@example.com", "StrongPassword123!", "Buyer", ReferralCode: owner.User.ReferralCode.ToLowerInvariant()), default);
            var outsider = await auth.RegisterAsync(new RegisterRequest("outsider@example.com", "StrongPassword123!", "Outsider"), default);
            await Assert.ThrowsAsync<BusinessException>(() => auth.RegisterAsync(new RegisterRequest("bad@example.com", "StrongPassword123!", "Bad", ReferralCode: "UNKNOWN"), default));
            Assert.Equal(owner.User.Id, (await db.Users.SingleAsync(x => x.Id == buyer.User.Id)).ReferredByUserId);
            db.AccountRoles.Add(new AccountRole { AccountId = other.User.AccountId, Role = "Admin" });
            var category = new Category { Name = "Products", Slug = "products", IsActive = true };
            var brand = new Brand { OwnerAccountId = other.User.AccountId, Name = "Brand", Slug = "brand", Status = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var product = new Product { StoreId = TestStores.Own(db, brand), BrandId = brand.Id, CategoryId = category.Id, Name = "Linked product", Slug = "linked", SKU = "LINKED", BasePrice = 100000, Status = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var unlinked = new Product { StoreId = TestStores.Own(db, brand), BrandId = brand.Id, CategoryId = category.Id, Name = "Unlinked product", Slug = "unlinked", SKU = "UNLINKED", BasePrice = 100000, Status = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var variant = new ProductVariant { ProductId = product.Id, SKU = "LINKED-1", VariantName = "Default", Price = 100000, StockQuantity = 20, IsActive = true };
            var secondVariant = new ProductVariant { ProductId = unlinked.Id, SKU = "UNLINKED-1", VariantName = "Default", Price = 100000, StockQuantity = 20, IsActive = true };
            db.Categories.Add(category); db.Brands.Add(brand); db.Products.AddRange(product, unlinked); db.ProductVariants.AddRange(variant, secondVariant); await db.SaveChangesAsync();
            var links = new AffiliateLinkService(store, config);
            (await db.AffiliateProfiles.SingleAsync(x => x.UserId == other.User.Id)).AffiliateCode = "LEGACY-AFFILIATE-ALIAS"; await db.SaveChangesAsync();
            var invitation = await links.InvitationAsync(owner.User.Id, default);
            Assert.Equal("https://shop.example.test/register?ref=" + owner.User.ReferralCode, invitation.Url);
            Assert.Equal(1, invitation.TotalInvites); Assert.Equal(0, invitation.SuccessfulInvites);
            var first = await links.CreateAsync(owner.User.Id, product.Id, default);
            var last = await links.CreateAsync(other.User.Id, product.Id, default);
            Assert.Equal(last.Code, (await links.CreateAsync(other.User.Id, product.Id, default)).Code);
            Assert.StartsWith("https://shop.example.test/product/", last.Url);
            Assert.Contains("ref=" + other.User.ReferralCode, last.Url);
            var oldVisit = await links.TrackAsync(first.Code, null, null, default);
            var visit = await links.TrackAsync(last.Code, oldVisit.Token, null, default);
            Assert.Equal(visit.Token, (await links.TrackAsync(last.Code, visit.Token, null, default)).Token);
            Assert.Equal(2, await db.AffiliateVisits.CountAsync());
            Assert.True((await db.AffiliateVisits.SingleAsync(x => x.AffiliateLinkId == first.Id)).ExpiresAt <= DateTimeOffset.UtcNow);
            Assert.NotEqual(visit.Token, (await db.AffiliateVisits.SingleAsync(x => x.AffiliateLinkId == last.Id)).TokenHash);
            await Assert.ThrowsAsync<BusinessException>(() => links.TrackAsync(last.Code, null, other.User.Id, default));
            await Assert.ThrowsAsync<BusinessException>(() => links.SetProductPolicyAsync(buyer.User.AccountId, product.Id, new(), default));
            await links.SetProductPolicyAsync(other.User.AccountId, product.Id, new(true, 0.01m, 700m), default);
            var member = new MemberService(store);
            var address = await member.SaveAddressAsync(buyer.User.Id, null, new AddressRequest("Buyer", "0901234567", "79", "001", "Test address", true), default);
            var finance = new FinanceService(store, config); var orders = new OrderService(store, config, finance);
            var voucher = new Voucher { Code = "LESS10", Name = "Discount", DiscountType = "FIXED", DiscountValue = 10000, PerUserLimit = 10, StartsAt = DateTimeOffset.UtcNow.AddDays(-1), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), Status = "ACTIVE" };
            db.Vouchers.Add(voucher); db.VoucherTargets.Add(new VoucherTarget { VoucherId = voucher.Id, ProductId = product.Id }); await db.SaveChangesAsync();
            await orders.AddAsync(buyer.User.Id, new(product.Id, variant.Id, 1), default); await orders.AddAsync(buyer.User.Id, new(unlinked.Id, secondVariant.Id, 1), default);
            var input = new CheckoutRequest(address.Id, VoucherCode: "LESS10");
            var order = await orders.CheckoutAsync(buyer.User.Id, "affiliate-checkout", input, default, visit.Token);
            Assert.Equal(order.Id, (await orders.CheckoutAsync(buyer.User.Id, "affiliate-checkout", input, default, oldVisit.Token)).Id);
            var attribution = await db.AffiliateAttributions.SingleAsync(); Assert.Equal(last.Id, attribution.AffiliateLinkId);
            Assert.Equal(buyer.User.Id, (await db.AffiliateVisits.SingleAsync(x => x.AffiliateLinkId == last.Id)).UserId);
            foreach (var status in new[] { "CONFIRMED", "READY", "SHIPPING" }) await orders.TransitionAsync(other.User.AccountId, order.Id, new(status), default);
            await orders.TransitionAsync(other.User.AccountId, order.Id, new("COMPLETED", true), default);
            var commission = await db.AffiliateCommissions.SingleAsync();
            Assert.Equal(90000, commission.BaseAmount); Assert.Equal(0.01m, commission.CommissionRate); Assert.Equal(700, commission.CommissionAmount); Assert.Equal("Đồng", commission.RankName);
            Assert.Equal((await db.AffiliateProfiles.SingleAsync(x => x.UserId == other.User.Id)).Id, commission.AffiliateProfileId);
            await using (var tx = await store.BeginTransactionAsync(default)) { await finance.AccrueOrderAsync(order.Id, default); await tx.CommitAsync(); }
            Assert.Single(await db.AffiliateCommissions.ToListAsync());
            await orders.AddAsync(buyer.User.Id, new(product.Id, variant.Id, 1), default);
            var exhausted = await orders.CheckoutAsync(buyer.User.Id, "budget-exhausted", new(address.Id), default, visit.Token);
            foreach (var status in new[] { "CONFIRMED", "READY", "SHIPPING" }) await orders.TransitionAsync(other.User.AccountId, exhausted.Id, new(status), default);
            await orders.TransitionAsync(other.User.AccountId, exhausted.Id, new("COMPLETED", true), default);
            Assert.Single(await db.AffiliateCommissions.ToListAsync());
            var history = (await finance.CommissionsAsync(other.User.Id, new CatalogQuery(), default)).Items.Single();
            Assert.Equal(order.OrderCode, history.OrderCode); Assert.Equal("Linked product", history.ProductName);
            await finance.ApproveCommissionAsync(other.User.AccountId, commission.Id, default);
            var withdrawal = await finance.WithdrawAsync(other.User.Id, "payout", new(700, "Test bank", "123456789", "TEST OWNER"), default);
            await finance.SetWithdrawalStatusAsync(other.User.AccountId, withdrawal, new("APPROVED"), default);
            await finance.SetWithdrawalStatusAsync(other.User.AccountId, withdrawal, new("PAID", "BANK-REF"), default);
            await finance.SetWithdrawalStatusAsync(other.User.AccountId, withdrawal, new("PAID", "BANK-REF"), default);
            Assert.Equal("PAID", commission.Status); Assert.Equal(700, commission.PaidAmount);
            await finance.ApproveCommissionAsync(other.User.AccountId, commission.Id, default);
            var refunds = new RefundSettlementService(store, config);
            var refund = await refunds.RequestRefundAsync(buyer.User.Id, order.Id, "refund", new("Returned goods"), default);
            await refunds.SetRefundStatusAsync(other.User.AccountId, refund, new("APPROVED"), default);
            await refunds.SetRefundStatusAsync(other.User.AccountId, refund, new("COMPLETED", "REFUND-REF", true), default);
            await refunds.SetRefundStatusAsync(other.User.AccountId, refund, new("COMPLETED", "REFUND-REF", true), default);
            Assert.Equal("REVERSED", commission.Status);
            Assert.Equal(700, (await db.AffiliateWallets.SingleAsync(x => x.AffiliateProfileId == commission.AffiliateProfileId)).DebtBalance);
            await orders.AddAsync(buyer.User.Id, new(product.Id, variant.Id, 1), default);
            var recovered = await orders.CheckoutAsync(buyer.User.Id, "budget-after-refund", new(address.Id), default, visit.Token);
            foreach (var status in new[] { "CONFIRMED", "READY", "SHIPPING" }) await orders.TransitionAsync(other.User.AccountId, recovered.Id, new(status), default);
            await orders.TransitionAsync(other.User.AccountId, recovered.Id, new("COMPLETED", true), default);
            var newCommission = await db.AffiliateCommissions.SingleAsync(x => x.Status == "PENDING"); Assert.Equal(700, newCommission.CommissionAmount);
            await finance.ApproveCommissionAsync(other.User.AccountId, newCommission.Id, default);
            Assert.Equal(0, (await db.AffiliateWallets.SingleAsync(x => x.AffiliateProfileId == commission.AffiliateProfileId)).DebtBalance);
            await orders.AddAsync(buyer.User.Id, new(product.Id, variant.Id, 1), default);
            var cancelled = await orders.CheckoutAsync(buyer.User.Id, "cancelled-affiliate", new(address.Id), default, visit.Token);
            await orders.CancelAsync(buyer.User.Id, cancelled.Id, default);
            Assert.Equal(2, await db.AffiliateCommissions.CountAsync());
            await using (var tx = await store.BeginTransactionAsync(default))
            {
                var attempt = new Order { UserId = outsider.User.Id };
                await AffiliateSources.CaptureAsync(store, attempt, new[] { new OrderItem { ProductId = product.Id } }, visit.Token, default);
                Assert.DoesNotContain(db.ChangeTracker.Entries<AffiliateAttribution>(), entry => entry.Entity.OrderId == attempt.Id);
            }
            var expiring = await links.TrackAsync(last.Code, null, null, default);
            await using (var tx = await store.BeginTransactionAsync(default))
            {
                var selfOrder = new Order { UserId = other.User.Id };
                await AffiliateSources.CaptureAsync(store, selfOrder, new[] { new OrderItem { ProductId = product.Id } }, expiring.Token, default);
                Assert.DoesNotContain(db.ChangeTracker.Entries<AffiliateAttribution>(), entry => entry.Entity.OrderId == selfOrder.Id);
            }
            (await db.AffiliateVisits.SingleAsync(x => x.TokenHash == AffiliateSources.Hash(expiring.Token))).ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1); await db.SaveChangesAsync();
            await using (var tx = await store.BeginTransactionAsync(default))
            {
                var attempt = new Order { UserId = outsider.User.Id };
                await AffiliateSources.CaptureAsync(store, attempt, new[] { new OrderItem { ProductId = product.Id } }, expiring.Token, default);
                Assert.DoesNotContain(db.ChangeTracker.Entries<AffiliateAttribution>(), entry => entry.Entity.OrderId == attempt.Id);
            }
            await links.SetProductPolicyAsync(other.User.AccountId, product.Id, new(true, 0.01m, 1400m), default);
            var raceVariant = new ProductVariant { ProductId = product.Id, SKU = "RACE-VARIANT", VariantName = "Race", Price = 100000, StockQuantity = 10, IsActive = true };
            db.ProductVariants.Add(raceVariant); await db.SaveChangesAsync();
            var outsiderAddress = await member.SaveAddressAsync(outsider.User.Id, null, new AddressRequest("Outsider", "0908888888", "79", "001", "Race address", true), default);
            var outsiderVisit = await links.TrackAsync(first.Code, null, null, default);
            await orders.AddAsync(buyer.User.Id, new(product.Id, variant.Id, 1), default);
            var raceFirst = await orders.CheckoutAsync(buyer.User.Id, "race-first", new(address.Id), default, visit.Token);
            await orders.AddAsync(outsider.User.Id, new(product.Id, raceVariant.Id, 1), default);
            var raceSecond = await orders.CheckoutAsync(outsider.User.Id, "race-second", new(outsiderAddress.Id), default, outsiderVisit.Token);
            foreach (var raceOrder in new[] { raceFirst, raceSecond })
                foreach (var status in new[] { "CONFIRMED", "READY", "SHIPPING" }) await orders.TransitionAsync(other.User.AccountId, raceOrder.Id, new(status), default);
            var barrier = new BudgetReadBarrier();
            async Task<bool> Complete(Guid orderId, bool synchronize)
            {
                var builder = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection);
                if (synchronize) builder.AddInterceptors(barrier);
                await using var concurrent = new ApplicationDbContext(builder.Options);
                var concurrentStore = new CommerceStore(concurrent);
                try { await new OrderService(concurrentStore, config, new FinanceService(concurrentStore, config)).TransitionAsync(other.User.AccountId, orderId, new("COMPLETED", true), default); return true; }
                catch (Exception ex) when (DatabaseConflicts.IsConcurrency(ex)) { return false; }
            }
            var results = await Task.WhenAll(Complete(raceFirst.Id, true), Complete(raceSecond.Id, true));
            Assert.Equal(2, barrier.Hits); Assert.Single(results, value => value);
            Assert.True(await Complete(results[0] ? raceSecond.Id : raceFirst.Id, false));
            Assert.Equal(1400, await db.AffiliateCommissions.Where(x => x.Status != "REVERSED").SumAsync(x => x.CommissionAmount));
            await links.SetProductPolicyAsync(other.User.AccountId, product.Id, new(false), default);
            await Assert.ThrowsAsync<BusinessException>(() => links.CreateAsync(owner.User.Id, product.Id, default));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class BudgetReadBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int hits;
        public int Hits => hits;
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("AffiliateCommissions", StringComparison.Ordinal) && command.CommandText.Contains("GROUP BY", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref hits) == 2) released.TrySetResult();
                await released.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }
}
