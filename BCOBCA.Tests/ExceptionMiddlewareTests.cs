using BCOBCA.API.Middlewares;
using BCOBCA.BLL.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class ExceptionMiddlewareTests
{
    [Theory]
    [InlineData("40001", 0)] [InlineData("40001", 1)] [InlineData("40001", 2)]
    [InlineData("40P01", 0)] [InlineData("40P01", 1)] [InlineData("40P01", 2)]
    public async Task WrappedDatabaseConflictsReturnRetryable409WithoutLeakingDetails(string sqlState, int wrappers)
    {
        Exception failure = new PostgresException("private database detail", "ERROR", "ERROR", sqlState);
        if (wrappers > 0) failure = new DbUpdateException("private SQL", failure);
        if (wrappers > 1) failure = new InvalidOperationException("transient failure", failure);
        Assert.True(DatabaseConflicts.IsConcurrency(failure));
        using var services = new ServiceCollection().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        await new ExceptionMiddleware(_ => Task.FromException(failure), NullLogger<ExceptionMiddleware>.Instance).InvokeAsync(context);
        Assert.Equal(409, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("private", body);
    }

    [Fact]
    public void UnrelatedDatabaseErrorsAreNotTreatedAsRetryableConflicts()
        => Assert.False(DatabaseConflicts.IsConcurrency(new InvalidOperationException("failure", new PostgresException("check violation", "ERROR", "ERROR", "23514"))));
}
