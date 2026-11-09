using System.Security.Cryptography;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class FinanceFlowTests
{
    [Fact]
    public async Task CodCompletionAccruesOnceAndWithdrawalsPreserveLedgerAndEncryptBankDetails()
    {
        EnvironmentBootstrap.Load();
        var adminConnection = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure a development PostgreSQL connection.");
        var databaseName = "bcobca_finance_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(adminConnection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName }.ConnectionString).Options;
            await using var db = new ApplicationDbContext(options); await db.Database.MigrateAsync();
            var config = new ConfigurationBuilder().AddEnvironmentVariables().AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Deliberately differs from database policy: legacy environment rate must be ignored.
                ["Affiliate:CommissionRate"] = "0.9", ["Affiliate:ApprovalHoldDays"] = "0", ["Membership:SpendPerPoint"] = "10000",
                ["Settlement:PlatformFeeRate"] = "0.1", ["Settlement:HoldDays"] = "0",
                ["Security:BankDataKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            }).Build();
            // Keep ledger/recovery amounts at 10%; rank pricing is tested separately.
            foreach (var rank in await db.Set<AffiliateRank>().ToListAsync()) rank.CommissionRate = 0.1m;
            await db.SaveChangesAsync();
            var store = new CommerceStore(db);
            var auth = new AuthService(store, config, new RegisterValidator(), new LoginValidator(), new ProfileValidator());
            var referrer = await auth.RegisterAsync(new RegisterRequest("referrer@example.com", "StrongPassword123!", "Test referrer"), default);
            var buyer = await auth.RegisterAsync(new RegisterRequest("buyer@example.com", "StrongPassword123!", "Test buyer", ReferralCode: referrer.User.ReferralCode), default);
            var finance = new FinanceService(store, config); var orders = new OrderService(store, config, finance); var members = new MemberService(store);
            var category = new Category { Name = "Test category", Slug = "test", IsActive = true };
            var brand = new Brand { Name = "Test brand", Slug = "test", OwnerAccountId = referrer.User.AccountId, Status = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var product = new Product { StoreId = TestStores.Own(db, brand), Name = "Test product", Slug = "test", BrandId = brand.Id, CategoryId = category.Id, SKU = "TEST-P", BasePrice = 100000, Status = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var variant = new ProductVariant { ProductId = product.Id, SKU = "TEST-V", VariantName = "Standard", Price = 100000, StockQuantity = 2, IsActive = true };
            db.Categories.Add(category); db.Brands.Add(brand); db.Products.Add(product); db.ProductVariants.Add(variant); await db.SaveChangesAsync();
            var address = await members.SaveAddressAsync(buyer.User.Id, null, new AddressRequest("Test buyer", "0901234567", "79", "001", "Test-only address", true), default);
            await orders.AddAsync(buyer.User.Id, new CartItemRequest(product.Id, variant.Id, 1), default);
            var order = await orders.CheckoutAsync(buyer.User.Id, "test-finance-order", new CheckoutRequest(address.Id), default);
            var denied = await Assert.ThrowsAsync<BusinessException>(() => orders.TransitionAsync(referrer.User.AccountId, order.Id, new OrderStatusRequest("CONFIRMED"), default));
            Assert.Equal(403, denied.StatusCode);
            db.AccountRoles.Add(new AccountRole { AccountId = referrer.User.AccountId, Role = "Admin" }); await db.SaveChangesAsync();
            foreach (var status in new[] { "CONFIRMED", "READY" }) await orders.TransitionAsync(referrer.User.AccountId, order.Id, new OrderStatusRequest(status), default);
            var operations = new AdminOperationsService(store, orders);
            var shipmentInput = new ShipmentInput(order.Id, " Test carrier ", " TEST-TRACKING ");
            await Assert.ThrowsAsync<BusinessException>(() => operations.ShipmentAsync(buyer.User.AccountId, shipmentInput, default));
            var shipmentId = await operations.ShipmentAsync(referrer.User.AccountId, shipmentInput, default);
            Assert.Equal(shipmentId, await operations.ShipmentAsync(referrer.User.AccountId, shipmentInput, default));
            Assert.Equal("TEST-TRACKING", (await orders.OrderAsync(buyer.User.Id, order.Id, default)).Shipments!.Single().TrackingCode);
            var secondShipment = await operations.ShipmentAsync(referrer.User.AccountId, shipmentInput with { TrackingCode = "TEST-SECOND" }, default);
            Assert.NotEqual(shipmentId, secondShipment);
            Assert.Equal(2, (await orders.OrderAsync(buyer.User.Id, order.Id, default)).Shipments!.Count);
            var feedbackId = await members.FeedbackAsync(buyer.User.Id, new FeedbackRequest("SUPPORT", "Test subject", "Test support request"), default);
            await operations.ReplyAsync(referrer.User.AccountId, feedbackId, new FeedbackReplyRequest("Test staff response"), default);
            Assert.Equal("RESOLVED", (await members.FeedbacksAsync(buyer.User.Id, new CatalogQuery(), default)).Items.Single().Status);
            Assert.Empty((await members.FeedbacksAsync(referrer.User.Id, new CatalogQuery(), default)).Items);
            await orders.TransitionAsync(referrer.User.AccountId, order.Id, new OrderStatusRequest("COMPLETED", true), default);
            Assert.Equal("PAID", (await db.Payments.SingleAsync()).Status);
            Assert.Equal(10, (await db.UserMemberships.SingleAsync(x => x.UserId == buyer.User.Id)).AvailablePoints);
            Assert.Equal(1, (await db.ProductVariants.SingleAsync()).StockQuantity);
            Assert.Equal(0, (await db.ProductVariants.SingleAsync()).ReservedQuantity);
            var reviews = new ReviewService(store);
            var purchasedItemId = order.Items.Single().Id;
            await Assert.ThrowsAsync<BusinessException>(() => reviews.SaveAsync(referrer.User.Id, purchasedItemId, new ReviewInput(5, "Not the purchasing user"), default));
            await Assert.ThrowsAsync<BusinessException>(() => reviews.SaveAsync(buyer.User.Id, purchasedItemId, new ReviewInput(6, "Invalid rating"), default));
            var reviewInput = new ReviewInput(5, "Test verified-purchase review", ["https://example.com/test-review.jpg"]);
            var reviewId = await reviews.SaveAsync(buyer.User.Id, purchasedItemId, reviewInput, default);
            Assert.Empty((await reviews.ReviewsAsync(product.Id, new CatalogQuery(), default)).Items);
            Assert.Equal(reviewId, (await reviews.MyReviewAsync(buyer.User.Id, purchasedItemId, default))!.Id);
            Assert.Contains((await reviews.PendingAsync(referrer.User.AccountId, new CatalogQuery(), default)).Items, x => x.Id == reviewId);
            var originalRevision = (await reviews.MyReviewAsync(buyer.User.Id, purchasedItemId, default))!.Revision;
            await Assert.ThrowsAsync<BusinessException>(() => reviews.ModerateAsync(buyer.User.AccountId, reviewId, 1, originalRevision, default));
            var changedReviewInput = reviewInput with { Content = "Edited content awaiting a fresh moderation" };
            await reviews.SaveAsync(buyer.User.Id, purchasedItemId, changedReviewInput, default);
            var staleModeration = await Assert.ThrowsAsync<BusinessException>(() => reviews.ModerateAsync(referrer.User.AccountId, reviewId, 1, originalRevision, default));
            Assert.Equal(409, staleModeration.StatusCode);
            Assert.Empty((await reviews.ReviewsAsync(product.Id, new CatalogQuery(), default)).Items);
            var currentRevision = (await reviews.MyReviewAsync(buyer.User.Id, purchasedItemId, default))!.Revision;
            await reviews.ModerateAsync(referrer.User.AccountId, reviewId, 1, currentRevision, default);
            Assert.Single((await reviews.ReviewsAsync(product.Id, new CatalogQuery(), default)).Items);
            Assert.Equal(reviewId, await reviews.SaveAsync(buyer.User.Id, purchasedItemId, changedReviewInput, default));
            Assert.Equal(1, (await db.ProductReviews.SingleAsync()).Status);
            Assert.Equal("QUALIFIED", (await db.AffiliateReferrals.SingleAsync()).Status);
            var profile = await db.AffiliateProfiles.SingleAsync(x => x.UserId == referrer.User.Id);
            var wallet = await db.AffiliateWallets.SingleAsync(x => x.AffiliateProfileId == profile.Id);
            Assert.Equal(10000, wallet.PendingBalance);
            await using (var tx = await store.BeginTransactionAsync(default)) { await finance.AccrueOrderAsync(order.Id, default); await tx.CommitAsync(); }
            Assert.Equal(1, await db.AffiliateCommissions.CountAsync());
            var commission = await db.AffiliateCommissions.SingleAsync();
            Assert.Equal(0.1m, commission.CommissionRate);
            var loyalty = new LoyaltyConfigurationService(store);
            var originalRanks = await loyalty.AffiliateRanksAsync(default);
            await loyalty.UpdateAffiliateRanksAsync(referrer.User.AccountId, new(originalRanks.Select(x => x with { CommissionRate = 0.08m }).ToArray()), default);
            Assert.Equal(0.1m, (await db.AffiliateCommissions.AsNoTracking().SingleAsync()).CommissionRate);
            Assert.Equal(10000, (await db.AffiliateCommissions.AsNoTracking().SingleAsync()).CommissionAmount);
            await loyalty.UpdateAffiliateRanksAsync(referrer.User.AccountId, new(originalRanks), default);
            await finance.ApproveCommissionAsync(referrer.User.AccountId, commission.Id, default);
            await finance.ApproveCommissionAsync(referrer.User.AccountId, commission.Id, default);
            Assert.Equal(10000, wallet.AvailableBalance); Assert.Equal(0, wallet.PendingBalance);
            var withdrawalInput = new WithdrawalRequest(5000, "Test bank", "123456789012", "TEST RECEIVER");
            var id = await finance.WithdrawAsync(referrer.User.Id, "test-withdrawal", withdrawalInput, default);
            Assert.Equal(id, await finance.WithdrawAsync(referrer.User.Id, "test-withdrawal", withdrawalInput, default));
            Assert.Equal(5000, wallet.AvailableBalance);
            var row = await db.AffiliateWithdrawals.SingleAsync();
            Assert.Equal("****9012", row.AccountNumber); Assert.NotEqual(withdrawalInput.AccountHolder, row.AccountHolder);
            Assert.DoesNotContain(withdrawalInput.AccountNumber, row.BankDetailsEncrypted);
            var details = await finance.BankDetailsAsync(referrer.User.AccountId, id, default);
            Assert.Equal(withdrawalInput.AccountNumber, details.AccountNumber);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => finance.BankDetailsAsync(buyer.User.AccountId, id, default))).StatusCode);
            Assert.Empty((await finance.WithdrawalsAsync(buyer.User.Id, new CatalogQuery(), default)).Items);
            await finance.SetWithdrawalStatusAsync(referrer.User.AccountId, id, new WithdrawalStatusRequest("REJECTED"), default);
            await finance.SetWithdrawalStatusAsync(referrer.User.AccountId, id, new WithdrawalStatusRequest("REJECTED"), default);
            Assert.Equal(10000, wallet.AvailableBalance);
            Assert.Equal(2, await db.AffiliateWalletTransactions.CountAsync(x => x.ReferenceId == id));
            var paidId = await finance.WithdrawAsync(referrer.User.Id, "test-paid-withdrawal", withdrawalInput with { Amount = 6000 }, default);
            await finance.SetWithdrawalStatusAsync(referrer.User.AccountId, paidId, new WithdrawalStatusRequest("APPROVED"), default);
            await finance.SetWithdrawalStatusAsync(referrer.User.AccountId, paidId, new WithdrawalStatusRequest("PAID", "TEST-BANK-TRANSFER-REFERENCE"), default);
            await finance.SetWithdrawalStatusAsync(referrer.User.AccountId, paidId, new WithdrawalStatusRequest("PAID", "TEST-BANK-TRANSFER-REFERENCE"), default);
            Assert.Equal(4000, wallet.AvailableBalance);
            await Assert.ThrowsAsync<BusinessException>(() => finance.SetWithdrawalStatusAsync(referrer.User.AccountId, paidId, new WithdrawalStatusRequest("REJECTED"), default));
            Assert.Equal(1, (await finance.LeaderboardAsync(new CatalogQuery(), default)).Items.Single(x => x.Id == profile.Id).Referrals);
            var refunds = new RefundSettlementService(store, config);
            var settlement = await db.SellerSettlements.SingleAsync();
            Assert.Equal(90000, settlement.NetAmount);
            await refunds.PaySettlementAsync(referrer.User.AccountId, settlement.Id, new SettlementPaymentRequest("TEST-SELLER-PAYMENT"), default);
            Assert.Equal(90000, settlement.PaidAmount);
            await Assert.ThrowsAsync<BusinessException>(() => refunds.RequestRefundAsync(referrer.User.Id, order.Id, "not-owned", new RefundRequest("Test reason"), default));
            var refundId = await refunds.RequestRefundAsync(buyer.User.Id, order.Id, "test-refund", new RefundRequest("Test full refund after real COD confirmation"), default);
            Assert.Equal(refundId, await refunds.RequestRefundAsync(buyer.User.Id, order.Id, "test-refund", new RefundRequest("Test full refund after real COD confirmation"), default));
            await refunds.SetRefundStatusAsync(referrer.User.AccountId, refundId, new RefundStatusRequest("APPROVED"), default);
            var refundCompletion = new RefundStatusRequest("COMPLETED", "TEST-CASH-REFUND", true);
            await refunds.SetRefundStatusAsync(referrer.User.AccountId, refundId, refundCompletion, default);
            await refunds.SetRefundStatusAsync(referrer.User.AccountId, refundId, refundCompletion, default);
            Assert.Equal("REFUNDED", (await db.Orders.SingleAsync()).Status);
            Assert.Equal(0, await db.PaymentTransactions.SumAsync(x => x.Amount));
            Assert.Equal(0, (await db.UserMemberships.SingleAsync(x => x.UserId == buyer.User.Id)).AvailablePoints);
            Assert.Equal("REVERSED", (await db.AffiliateCommissions.SingleAsync()).Status);
            Assert.Equal(0, wallet.AvailableBalance); Assert.Equal(6000, wallet.DebtBalance);
            Assert.Equal(2, (await db.ProductVariants.SingleAsync()).StockQuantity);
            Assert.Equal(1, await db.InventoryMovements.CountAsync(x => x.Type == "RETURN"));
            Assert.Equal("PAID", settlement.Status); Assert.Equal(90000, settlement.PaidAmount);
            var adjustment = await db.SellerSettlementAdjustments.SingleAsync(); Assert.Equal(90000, adjustment.RemainingAmount);
            await Assert.ThrowsAsync<BusinessException>(() => finance.WithdrawAsync(referrer.User.Id, "must-not-withdraw-debt", withdrawalInput with { Amount = 1 }, default));
            await orders.AddAsync(buyer.User.Id, new CartItemRequest(product.Id, variant.Id, 1), default);
            var nextOrder = await orders.CheckoutAsync(buyer.User.Id, "test-next-order", new CheckoutRequest(address.Id), default);
            foreach (var status in new[] { "CONFIRMED", "READY", "SHIPPING" }) await orders.TransitionAsync(referrer.User.AccountId, nextOrder.Id, new OrderStatusRequest(status), default);
            await orders.TransitionAsync(referrer.User.AccountId, nextOrder.Id, new OrderStatusRequest("COMPLETED", true), default);
            var nextCommission = await db.AffiliateCommissions.SingleAsync(x => x.Status == "PENDING");
            var disputed = await refunds.RequestRefundAsync(buyer.User.Id, nextOrder.Id, "test-pending-dispute", new RefundRequest("Test freeze while refund is under review"), default);
            await Assert.ThrowsAsync<BusinessException>(() => finance.ApproveCommissionAsync(referrer.User.AccountId, nextCommission.Id, default));
            await refunds.SetRefundStatusAsync(referrer.User.AccountId, disputed, new RefundStatusRequest("REJECTED"), default);
            await finance.ApproveCommissionAsync(referrer.User.AccountId, nextCommission.Id, default);
            Assert.Equal(0, wallet.DebtBalance); Assert.Equal(4000, wallet.AvailableBalance);
            var nextSettlement = await db.SellerSettlements.SingleAsync(x => x.Status == "PENDING");
            await refunds.PaySettlementAsync(referrer.User.AccountId, nextSettlement.Id, new SettlementPaymentRequest(""), default);
            Assert.Equal(0, nextSettlement.PaidAmount); Assert.StartsWith("OFFSET:", nextSettlement.TransactionReference);
            Assert.Equal(0, adjustment.RemainingAmount); Assert.Equal("RECOVERED", adjustment.Status);
            Assert.Equal(90000, (await db.SellerSettlementRecoveries.SingleAsync()).Amount);
            var secondRefundId = await refunds.RequestRefundAsync(buyer.User.Id, nextOrder.Id, "test-refund-offset-order", new RefundRequest("Test reversal of an offset settlement"), default);
            await refunds.SetRefundStatusAsync(referrer.User.AccountId, secondRefundId, new RefundStatusRequest("APPROVED"), default);
            await refunds.SetRefundStatusAsync(referrer.User.AccountId, secondRefundId, new RefundStatusRequest("COMPLETED", "TEST-SECOND-REFUND", false), default);
            Assert.Equal(90000, (await db.SellerSettlementAdjustments.SingleAsync(x => x.RefundId == secondRefundId)).RemainingAmount);
            Assert.Equal(1, (await db.ProductVariants.SingleAsync()).StockQuantity);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
