using BCOBCA.BLL.Common;
using Xunit;

namespace BCOBCA.Tests;

public sealed class GhnLifecycleRulesTests
{
    [Theory]
    [InlineData("ready_to_pick", false)]
    [InlineData("picking", false)]
    [InlineData("picked", true)]
    [InlineData("storing", true)]
    [InlineData("delivering", true)]
    [InlineData("delivery_fail", true)]
    public void OnlyCarrierPickupAdvancesToShipping(string status, bool picked)
        => Assert.Equal(picked, GhnLifecycleRules.HasPickedUp(status));

    [Theory]
    [InlineData("delivered")]
    [InlineData("returned")]
    [InlineData("cancel")]
    [InlineData("lost")]
    public void TerminalProviderStatesCannotRegress(string status)
        => Assert.True(GhnLifecycleRules.IsTerminal(status));

    [Fact]
    public void UnknownProviderStatusesAreRejected()
        => Assert.Throws<BusinessException>(() => GhnLifecycleRules.Label("unexpected"));
}
