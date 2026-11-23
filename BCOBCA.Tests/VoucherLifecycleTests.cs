using BCOBCA.BLL.Common;
using Xunit;
namespace BCOBCA.Tests;
public sealed class VoucherLifecycleTests
{
    [Theory]
    [InlineData("INACTIVE", -1, 1, 1, 0, "DISABLED")]
    [InlineData("ACTIVE", -2, -1, 1, 0, "EXPIRED")]
    [InlineData("ACTIVE", -1, 1, 1, 1, "EXHAUSTED")]
    [InlineData("ACTIVE", 1, 2, 10, 0, "UPCOMING")]
    [InlineData("ACTIVE", -1, 1, 10, 0, "ACTIVE")]
    public void ComputesStateWithoutScheduledJobs(string status, int start, int end, int limit, int used, string expected)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(expected, VoucherLifecycle.Status(status, now.AddDays(start), now.AddDays(end), limit, used, now));
    }
}
