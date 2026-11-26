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
            var user = new User { AccountId = owner.Id, FullName = "Owner Search", ReferralCode = "STORES" };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Pending store", Slug = "pending-store", StoreType = 1, Status = 0, Phone = "0912345678", CreatedAt = DateTimeOffset.UtcNow.AddDays(-2) };
            var active = new Store { OwnerAccountId = owner.Id, Name = "Active store", Slug = "active-store", StoreType = 1, Status = 1, IsVerified = true, CreatedAt = DateTimeOffset.UtcNow.AddDays(-20) };
            var hidden = new Store { OwnerAccountId = owner.Id, Name = "Deleted", Slug = "deleted-store", StoreType = 1, Status = 1, DeletedAt = DateTimeOffset.UtcNow };
            var brandStore = new Store { OwnerAccountId = owner.Id, Name = "Other type", Slug = "other-type", StoreType = 0, Status = 1 };
            var verification = new StoreVerification { StoreId = shop.Id, Status = 0, LegalName = "Store Company", TaxCode = "1234567890", Documents = System.Text.Json.JsonSerializer.Serialize(new[] { new VerificationDocument("license", "license.png", "image/png", 3, "private-document") }) };
            var brand = new Brand { OwnerAccountId = owner.Id, Name = "Brand", Slug = "admin-stores-brand", Status = 1 };
            var order = new Order { UserId = user.Id, OrderCode = "STORES-ORDER", Status = "COMPLETED", PaymentStatus = "PAID", Subtotal = 1000000, TotalAmount = 1010000, IdempotencyKey = "admin-stores-order", CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) };
            var sellerOrder = new SellerOrder { StoreId = shop.Id, BrandId = brand.Id, OrderId = order.Id, TotalAmount = 40000, CreatedAt = order.CreatedAt };
            db.AddRange(admin, owner, user, shop, active, hidden, brandStore, verification, brand, order, sellerOrder);
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
            var doc = await service.DocumentAsync(admin.Id, shop.Id, verification.Id, "license", default);
            Assert.Equal("AQID", doc.Content); Assert.Equal(1, storage.Reads);
            Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => service.DocumentAsync(admin.Id, active.Id, verification.Id, "license", default))).StatusCode);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.SummaryAsync(owner.Id, default))).StatusCode);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.DocumentAsync(owner.Id, shop.Id, verification.Id, "license", default))).StatusCode);
            await Assert.ThrowsAsync<BusinessException>(() => service.ListAsync(admin.Id, new(PageSize: 500), default));
            await Assert.ThrowsAsync<BusinessException>(() => service.StatusAsync(admin.Id, shop.Id, new(true, "", detail.Store.UpdatedAt), default));
            await Assert.ThrowsAsync<BusinessException>(() => service.ReviewAsync(admin.Id, shop.Id, new(verification.Id, false, "", detail.Store.UpdatedAt), default));
            var otherDetail = await service.DetailAsync(admin.Id, active.Id, 7, default);
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
