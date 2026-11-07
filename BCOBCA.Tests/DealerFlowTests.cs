using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class DealerFlowTests
{
    [Fact]
    public async Task DealersUseRealCountsRespectPrivacyAndSupportIdempotentFollows()
    {
        EnvironmentBootstrap.Load();
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure development PostgreSQL.");
        var scratch = new NpgsqlConnectionStringBuilder(supplied) { Database = "bcobca_dealer_test_" + Guid.NewGuid().ToString("N") };
        await using var admin = new NpgsqlConnection(supplied);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{scratch.Database}\" TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'vi-VN'", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var now = DateTimeOffset.UtcNow;
            var account = new Account { Email = "dealer@example.test", Phone = "0900000099", PasswordHash = "test", Role = "Customer", Status = 1, CreatedAt = now, UpdatedAt = now };
            db.Accounts.Add(account);
            var dealer = new Store { OwnerAccountId = account.Id, StoreType = 1, Name = "Đại lý An", Provinces = "Hồ Chí Minh", Slug = "dai-ly-an", Status = 1, IsVerified = true, Phone = "0900000099", Email = "private@example.test", CreatedAt = now, UpdatedAt = now };
            var second = new Store { OwnerAccountId = account.Id, StoreType = 1, Name = "Bình", Slug = "binh", Status = 1, CreatedAt = now.AddDays(1), UpdatedAt = now };
            var personal = new Store { OwnerAccountId = account.Id, Name = "Cá nhân", Slug = "ca-nhan", Status = 1, CreatedAt = now, UpdatedAt = now };
            db.Stores.AddRange(dealer, second, personal,
                new Store { OwnerAccountId = account.Id, StoreType = 1, Name = "Pending", Slug = "pending" },
                new Store { OwnerAccountId = account.Id, StoreType = 1, Status = 1, Slug = "deleted", DeletedAt = now });
            db.StoreSettings.Add(new StoreSettings { StoreId = dealer.Id, ShowBranches = true, ShowPhone = true, ShowEmail = false, UpdatedAt = now });
            db.StoreBranches.AddRange(
                new StoreBranch { StoreId = dealer.Id, Name = "Chi nhánh chính", Address = "Địa chỉ thật", ProvinceCode = "79", IsMain = true, IsActive = true, CreatedAt = now, UpdatedAt = now },
                new StoreBranch { StoreId = dealer.Id, Name = "Đã đóng", Address = "Địa chỉ đã đóng", ProvinceCode = "01", IsActive = false, CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();
            Assert.Single(await db.StoreBranches.Where(x => x.StoreId == personal.Id && x.IsMain).ToListAsync());
            var service = new DealerService(new CommerceStore(db));
            var page = await service.ListAsync(new DealerQuery(), null, default);
            Assert.Equal(2, page.TotalItems);
            Assert.All(page.Items, x => Assert.False(x.IsFollowing));
            Assert.Equal("Hồ Chí Minh", page.Items.Single(x => x.Id == dealer.Id).LocationLabel);
            Assert.Null(page.Items.Single(x => x.Id == second.Id).Provinces);
            Assert.Equal("Chưa thiết lập phạm vi", page.Items.Single(x => x.Id == second.Id).LocationLabel);
            Assert.Contains(await service.OwnedAsync(account.Id, default), x => x.Id == dealer.Id);
            Assert.Empty(await service.OwnedAsync(Guid.NewGuid(), default));
            var forbidden = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateProvincesAsync(dealer.Id, Guid.NewGuid(), new("Toàn quốc"), default));
            Assert.Equal(404, forbidden.StatusCode);
            await Assert.ThrowsAsync<BusinessException>(() => service.UpdateProvincesAsync(personal.Id, account.Id, new("Toàn quốc"), default));
            await Assert.ThrowsAsync<BusinessException>(() => service.UpdateProvincesAsync(dealer.Id, account.Id, new("Toàn quốc, Hà Nội"), default));
            var saved = await service.UpdateProvincesAsync(dealer.Id, account.Id, new(" Hà Nội, Hồ Chí Minh, Đà Nẵng, Hà Nội "), default);
            Assert.Equal("Hà Nội, Hồ Chí Minh, Đà Nẵng", saved.Provinces);
            Assert.Single((await service.ListAsync(new DealerQuery(ProvinceCode: "Đà Nẵng"), null, default)).Items);
            Assert.Empty((await service.ListAsync(new DealerQuery(ProvinceCode: "Huế"), null, default)).Items);
            await service.UpdateProvincesAsync(second.Id, account.Id, new("Toàn quốc"), default);
            Assert.Equal(2, (await service.ListAsync(new DealerQuery(ProvinceCode: "Hà Nội"), null, default)).TotalItems);
            Assert.Single((await service.ListAsync(new DealerQuery(ProvinceCode: "Huế"), null, default)).Items);
            Assert.Equal("Toàn quốc", (await service.DetailAsync(second.Slug, null, default)).Dealer.Provinces);
            await service.UpdateProvincesAsync(dealer.Id, account.Id, new("Hồ Chí Minh"), default);
            second.Provinces = null;
            await db.SaveChangesAsync();
            var mainBranch = db.StoreBranches.Local.Single(x => x.StoreId == dealer.Id && x.IsMain);
            mainBranch.Address = " ";
            await db.SaveChangesAsync();
            Assert.Equal("Hồ Chí Minh", (await service.ListAsync(new DealerQuery(Search: "Đại"), null, default)).Items[0].LocationLabel);
            db.StoreBranches.Add(new StoreBranch { StoreId = dealer.Id, Name = "Chi nhánh phụ", Address = "Địa chỉ chi nhánh phụ", ProvinceCode = "79", IsActive = true });
            await db.SaveChangesAsync();
            Assert.Equal("Hồ Chí Minh", (await service.ListAsync(new DealerQuery(Search: "Đại"), null, default)).Items[0].LocationLabel);
            mainBranch.Address = "Địa chỉ thật";
            await db.SaveChangesAsync();
            Assert.Equal("Hồ Chí Minh", (await service.ListAsync(new DealerQuery(Search: "Đại"), null, default)).Items[0].LocationLabel);
            Assert.Single((await service.ListAsync(new DealerQuery(Search: "ĐẠI LÝ", ProvinceCode: "79", VerifiedOnly: true), null, default)).Items);
            Assert.Empty((await service.ListAsync(new DealerQuery(ProvinceCode: "01"), null, default)).Items);
            Assert.Equal(second.Id, (await service.ListAsync(new DealerQuery(SortBy: "newest", PageSize: 1), null, default)).Items[0].Id);
            Assert.Equal(2, (await service.ListAsync(new DealerQuery(PageSize: 1), null, default)).TotalPages);
            await Assert.ThrowsAsync<BusinessException>(() => service.ListAsync(new DealerQuery(PageSize: 51), null, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.ListAsync(new DealerQuery(Page: int.MaxValue), null, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.DetailAsync("pending", null, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.DetailAsync("deleted", null, default));
            var followed = await service.FollowAsync(dealer.Id, account.Id, true, default);
            Assert.True(followed.IsFollowing);
            Assert.Equal(1, followed.FollowerCount);
            await service.FollowAsync(dealer.Id, account.Id, true, default);
            Assert.Single(await db.StoreFollowers.ToListAsync());
            Assert.Equal(dealer.Id, (await service.ListAsync(new DealerQuery(), account.Id, default)).Items[0].Id);
            await using (var concurrentDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options))
            await using (var otherDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options))
            {
                await Task.WhenAll(new DealerService(new CommerceStore(concurrentDb)).FollowAsync(second.Id, account.Id, true, default),
                    new DealerService(new CommerceStore(otherDb)).FollowAsync(second.Id, account.Id, true, default));
                Assert.Equal(1, await db.StoreFollowers.CountAsync(x => x.StoreId == second.Id));
            }
            var detail = await service.DetailAsync(dealer.Slug, account.Id, default);
            Assert.True(detail.Dealer.IsFollowing);
            Assert.Null(detail.Email);
            Assert.Equal(dealer.Phone, detail.Phone);
            Assert.Equal(2, detail.Branches.Count);
            Assert.Equal(DealerProvinces.Names, await service.RegionsAsync(default));
            var settings = await db.StoreSettings.SingleAsync();
            settings.ShowBranches = false;
            await db.SaveChangesAsync();
            Assert.Empty((await service.DetailAsync(dealer.Slug, null, default)).Branches);
            Assert.Equal("Hồ Chí Minh", (await service.ListAsync(new DealerQuery(Search: "Đại"), null, default)).Items[0].LocationLabel);
            Assert.Equal(DealerProvinces.Names, await service.RegionsAsync(default));
            Assert.False((await service.FollowAsync(dealer.Id, account.Id, false, default)).IsFollowing);
            Assert.Equal(0, (await service.FollowAsync(dealer.Id, account.Id, false, default)).FollowerCount);
            var hardDelete = await Assert.ThrowsAsync<PostgresException>(() => db.Stores.Where(x => x.Id == second.Id).ExecuteDeleteAsync());
            Assert.Equal("23514", hardDelete.SqlState);
            db.Stores.Remove(dealer);
            await db.SaveChangesAsync();
            Assert.NotNull((await db.Stores.IgnoreQueryFilters().SingleAsync(x => x.Id == dealer.Id)).DeletedAt);
            await Assert.ThrowsAsync<BusinessException>(() => service.FollowAsync(dealer.Id, account.Id, true, default));

            async Task Constraint(Action<ApplicationDbContext> arrange, string sqlState)
            {
                await using var check = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options);
                arrange(check);
                var error = await Assert.ThrowsAsync<DbUpdateException>(() => check.SaveChangesAsync());
                Assert.Equal(sqlState, Assert.IsType<PostgresException>(error.InnerException).SqlState);
            }
            await Constraint(check => check.Stores.Add(new Store { OwnerAccountId = account.Id, StoreType = 7, Slug = "bad-type" }), "23514");
            await Constraint(check => check.Stores.Add(new Store { OwnerAccountId = account.Id, StoreType = 1, Slug = "binh" }), "23505");
            await Constraint(check => check.StoreBranches.Add(new StoreBranch { StoreId = Guid.NewGuid() }), "23503");
            await Constraint(check => check.StoreBranches.Add(new StoreBranch { StoreId = personal.Id, IsMain = true }), "23514");
            await Constraint(check => check.StoreBranches.Add(new StoreBranch { StoreId = dealer.Id, IsMain = true }), "23505");
            await Constraint(check => check.StoreVerifications.Add(new StoreVerification { StoreId = second.Id, Documents = "{}" }), "23514");
            db.StoreBranches.Add(new StoreBranch { StoreId = second.Id, ManagerAccountId = account.Id });
            await db.SaveChangesAsync();
            await Constraint(check => check.StoreBranches.Add(new StoreBranch { StoreId = dealer.Id, ManagerAccountId = account.Id }), "23505");
            await db.GetService<IMigrator>().MigrateAsync("20261105032258_RemoveCategoryIconUrl");
            Assert.Equal(1, await db.Accounts.CountAsync());
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Stores.ToListAsync());
            Assert.Equal(1, await db.Accounts.CountAsync());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{scratch.Database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
