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
            var operatorAccount = new Account { Email = "support-admin@example.test", Role = "Admin", Status = 1 };
            db.Add(operatorAccount); await db.SaveChangesAsync();
            var operations = new AdminOperationsService(new CommerceStore(db), null!);
            var inbox = await operations.SupportAsync(operatorAccount.Id, new AdminSupportQuery { Search = " VOUCHER ", Subject = wanted.Subject, Status = "RESOLVED" }, default);
            Assert.Equal(wanted.Id, Assert.Single(inbox.Items).Id);
            Assert.Equal(2, inbox.Summary.Total); Assert.Equal(1, inbox.Summary.Open); Assert.Equal(1, inbox.Summary.Resolved);
            Assert.Equal(2, inbox.Subjects.Count);
            var byCode = await operations.SupportAsync(operatorAccount.Id, new AdminSupportQuery { Search = "#HT-" + wanted.Id.ToString()[..8] }, default);
            Assert.Equal(wanted.Id, Assert.Single(byCode.Items).Id);
            var openInbox = await operations.SupportAsync(operatorAccount.Id, new AdminSupportQuery { Status = "OPEN", Page = 100 }, default);
            Assert.Equal("OPEN", Assert.Single(openInbox.Items).Status); Assert.Equal(1, openInbox.Page);
            await Assert.ThrowsAsync<BCOBCA.BLL.Common.BusinessException>(() => operations.SupportAsync(account.Id, new(), default));
            await operations.ReplyAsync(operatorAccount.Id, wanted.Id, new FeedbackReplyRequest("Đã xác nhận hỗ trợ qua admin"), default);
            Assert.Equal("Đã xác nhận hỗ trợ qua admin", Assert.Single((await members.FeedbacksAsync(user.Id, new CatalogQuery(Search: "voucher"), default)).Items).AdminResponse);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{scratch.Database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
