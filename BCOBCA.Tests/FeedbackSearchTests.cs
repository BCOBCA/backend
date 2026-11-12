using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class FeedbackSearchTests
{
    [Fact]
    public async Task SearchesOwnedSubjectContentAndReplyBeforePagination()
    {
        EnvironmentBootstrap.Load();
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure isolated PostgreSQL tests.");
        var scratch = new NpgsqlConnectionStringBuilder(supplied) { Database = "bcobca_feedback_search_" + Guid.NewGuid().ToString("N") };
        await using var admin = new NpgsqlConnection(supplied); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{scratch.Database}\" TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'vi-VN'", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var account = new Account { Email = "feedback-search@example.test" };
            var user = new User { AccountId = account.Id, ReferralCode = "FEEDBACK-SEARCH" };
            var wanted = new Feedback { UserId = user.Id, Subject = "Ưu đãi và voucher", Content = "Mã giảm giá biến mất", AdminResponse = "Đã khôi phục", Status = "RESOLVED", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
            db.AddRange(account, user, wanted, new Feedback { UserId = user.Id, Subject = "Other", Content = "Other content", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            var members = new MemberService(new CommerceStore(db));
            foreach (var search in new[] { " VOUCHER ", "giảm giá", "khôi phục" })
            {
                var page = await members.FeedbacksAsync(user.Id, new CatalogQuery(PageSize: 1, Search: search), default);
                Assert.Equal(wanted.Id, Assert.Single(page.Items).Id); Assert.Equal(1, page.TotalItems);
            }
            Assert.Empty((await members.FeedbacksAsync(user.Id, new CatalogQuery(Search: "missing-query"), default)).Items);
            Assert.Empty((await members.FeedbacksAsync(Guid.NewGuid(), new CatalogQuery(Search: "voucher"), default)).Items);
            Assert.Equal(2, (await members.FeedbacksAsync(user.Id, new CatalogQuery(Search: "  "), default)).TotalItems);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{scratch.Database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
