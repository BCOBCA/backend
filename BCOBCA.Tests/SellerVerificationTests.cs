using System.Collections.Concurrent;
using System.Text.Json;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerVerificationTests
{
    // Deliberately independent of backend/.env: these tests must never create databases on production.
    private const string Local = "Host=127.0.0.1;Port=55433;Database=postgres;Username=postgres;Pooling=false;Timeout=5";
    private static SellerVerificationService Service(ApplicationDbContext db, Storage storage)
    {
        var store = new CommerceStore(db);
        return new(store, new SellerService(store), storage, NullLogger<SellerVerificationService>.Instance);
    }

    private static SellerVerificationInput Input(DateTimeOffset version) => new(Guid.NewGuid(), " Công ty An ", "0123456789", " Nguyễn An ", version,
        VerificationDocuments.Kinds.Select(k => new VerificationFileInput(k, k + ".pdf", "application/pdf", "%PDF-1.7\nsynthetic\n%%EOF"u8.ToArray())).ToArray());

    [Fact]
    public async Task OwnerSubmitsRetriesDownloadsAndResubmitsAfterRejection()
    {
        await WithDatabase(async options =>
        {
            await using var db = new ApplicationDbContext(options);
            var owner = new Account { Email = "verify-owner@example.test", Role = "Seller", Status = 1 };
            var other = new Account { Email = "verify-other@example.test", Role = "Seller", Status = 1 };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Shop", Slug = "verification-shop", StoreType = 1, Status = 1 };
            db.AddRange(owner, other, shop); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var seller = new SellerService(new CommerceStore(db));
            var initial = await seller.ProfileAsync(owner.Id, shop.Id, default);
            var storage = new Storage(); var service = Service(db, storage);
            var input = Input(initial.UpdatedAt);
            await Assert.ThrowsAsync<BusinessException>(() => service.SubmitAsync(other.Id, shop.Id, input, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.SubmitAsync(owner.Id, shop.Id, input with { Files = input.Files.Take(2).ToArray() }, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.SubmitAsync(owner.Id, shop.Id, input with { LegalName = "" }, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.SubmitAsync(owner.Id, shop.Id, input with { UpdatedAt = input.UpdatedAt.AddDays(-1) }, default));
            Assert.Empty(storage.Files);
            var submitted = await service.SubmitAsync(owner.Id, shop.Id, input, default);
            Assert.NotNull(submitted.Verification); Assert.Equal(0, submitted.Verification.Status);
            Assert.Equal("Công ty An", submitted.Verification.LegalName);
            Assert.Equal("Nguyễn An", submitted.Verification.RepresentativeName);
            Assert.Equal(input.SubmissionId, submitted.Verification.Id);
            Assert.Equal(3, submitted.Verification.Documents!.Count);
            Assert.False(submitted.IsVerified); Assert.Null(submitted.VerifiedAt);
            Assert.DoesNotContain("PublicId", JsonSerializer.Serialize(submitted));
            Assert.DoesNotContain("bcobca/verifications", JsonSerializer.Serialize(submitted));
            var retry = await service.SubmitAsync(owner.Id, shop.Id, input, default);
            Assert.Equal(input.SubmissionId, retry.Verification!.Id); Assert.Equal(3, storage.UploadCount);
            await Assert.ThrowsAsync<BusinessException>(() => service.SubmitAsync(owner.Id, shop.Id, Input(submitted.UpdatedAt), default));
            var download = await service.DownloadAsync(owner.Id, shop.Id, input.SubmissionId, "license", default);
            Assert.Equal(input.Files[0].Content, download.Content); Assert.Equal("application/pdf", download.ContentType);
            await Assert.ThrowsAsync<BusinessException>(() => service.DownloadAsync(other.Id, shop.Id, input.SubmissionId, "license", default));
            await Assert.ThrowsAsync<BusinessException>(() => service.DownloadAsync(owner.Id, shop.Id, input.SubmissionId, "missing", default));

            db.ChangeTracker.Clear();
            var rejected = await db.Set<StoreVerification>().SingleAsync();
            rejected.Status = 2; rejected.RejectionReason = "Giấy tờ không rõ."; rejected.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var reloaded = await seller.ProfileAsync(owner.Id, shop.Id, default);
            Assert.Equal("Giấy tờ không rõ.", reloaded.Verification!.RejectionReason);
            var replacement = await service.SubmitAsync(owner.Id, shop.Id, Input(reloaded.UpdatedAt), default);
            Assert.Equal(0, replacement.Verification!.Status); Assert.Null(replacement.Verification.RejectionReason);
            Assert.Equal(2, await db.Set<StoreVerification>().CountAsync());
            Assert.Equal(6, storage.UploadCount);
            db.ChangeTracker.Clear();
            var persisted = await db.Stores.SingleAsync(); persisted.IsVerified = true;
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<BusinessException>(() => service.SubmitAsync(owner.Id, shop.Id, Input(replacement.UpdatedAt), default));
            Assert.Equal(6, storage.UploadCount);
        });
    }

    [Fact]
    public async Task FailedUploadCleansAttemptAndDoesNotCreateApplication()
    {
        await WithDatabase(async options =>
        {
            await using var db = new ApplicationDbContext(options);
            var account = new Account { Email = "failure@example.test", Role = "Seller", Status = 1 };
            var shop = new Store { OwnerAccountId = account.Id, Name = "Shop", Slug = "failed-verification", StoreType = 1, Status = 1 };
            db.AddRange(account, shop); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var before = await new SellerService(new CommerceStore(db)).ProfileAsync(account.Id, shop.Id, default);
            var storage = new Storage { FailAt = 2 };
            await Assert.ThrowsAsync<BusinessException>(() => Service(db, storage).SubmitAsync(account.Id, shop.Id, Input(before.UpdatedAt), default));
            Assert.Empty(storage.Files); Assert.Equal(3, storage.DeleteCount);
            Assert.Empty(await db.Set<StoreVerification>().ToListAsync());
            Assert.Equal(before.UpdatedAt, (await db.Stores.AsNoTracking().SingleAsync()).UpdatedAt);
        });
    }

    [Fact]
    public async Task ConcurrentSubmissionsSaveOnlyOnePendingApplication()
    {
        await WithDatabase(async options =>
        {
            await using var setup = new ApplicationDbContext(options);
            var account = new Account { Email = "concurrent@example.test", Role = "Seller", Status = 1 };
            var shop = new Store { OwnerAccountId = account.Id, Name = "Shop", Slug = "concurrent-verification", StoreType = 1, Status = 1 };
            setup.AddRange(account, shop); await setup.SaveChangesAsync();
            var version = (await new SellerService(new CommerceStore(setup)).ProfileAsync(account.Id, shop.Id, default)).UpdatedAt;
            var storage = new Storage { Synchronize = true };
            async Task<Exception?> Submit()
            {
                await using var db = new ApplicationDbContext(options);
                return await Record.ExceptionAsync(() => Service(db, storage).SubmitAsync(account.Id, shop.Id, Input(version), default));
            }
            var results = await Task.WhenAll(Submit(), Submit());
            Assert.Single(results, e => e is null);
            var failure = Assert.Single(results, e => e is not null);
            Assert.True(failure is BusinessException || DatabaseConflicts.IsConcurrency(failure!));
            Assert.Single(await setup.Set<StoreVerification>().AsNoTracking().ToListAsync());
            Assert.Equal(3, storage.Files.Count);
        });
    }

    private sealed class Storage : IVerificationDocumentStorage
    {
        public ConcurrentDictionary<string, byte[]> Files { get; } = new();
        public int UploadCount; public int DeleteCount; public int FailAt; public bool Synchronize;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task UploadAsync(string publicId, VerificationFileInput file, CancellationToken ct)
        {
            var count = Interlocked.Increment(ref UploadCount);
            Files[publicId] = file.Content;
            if (count == FailAt) throw new BusinessException("Synthetic storage failure", 502);
            if (Synchronize && file.Kind == "identityBack")
            {
                if (count == 6) ready.TrySetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            }
        }
        public Task DeleteAsync(string publicId, CancellationToken ct) { Files.TryRemove(publicId, out _); Interlocked.Increment(ref DeleteCount); return Task.CompletedTask; }
        public Task<byte[]> ReadAsync(VerificationDocument document, CancellationToken ct) => Task.FromResult(Files[document.PublicId]);
    }

    internal static async Task WithDatabase(Func<DbContextOptions<ApplicationDbContext>, Task> action)
    {
        var database = "bcobca_test_verification_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(Local); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(Local) { Database = database }.ConnectionString).Options;
            await using (var db = new ApplicationDbContext(options)) await db.Database.MigrateAsync();
            await action(options);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
