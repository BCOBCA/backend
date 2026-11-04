using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using Xunit;

namespace BCOBCA.Tests;

public sealed class MembershipLevelTests
{
    private static IReadOnlyList<MembershipLevelDto> Levels => new[] { 0, 100, 300, 600, 1000, 1600, 2500, 4000, 6000, 10000 }
        .Select((points, index) => new MembershipLevelDto(index + 1, points, Array.Empty<string>())).ToArray();

    [Theory]
    [InlineData(0, 1)] [InlineData(99, 1)] [InlineData(100, 2)] [InlineData(299, 2)]
    [InlineData(300, 3)] [InlineData(599, 3)] [InlineData(600, 4)] [InlineData(999, 4)]
    [InlineData(1000, 5)] [InlineData(1599, 5)] [InlineData(1600, 6)] [InlineData(2499, 6)]
    [InlineData(2500, 7)] [InlineData(3999, 7)] [InlineData(4000, 8)] [InlineData(5999, 8)]
    [InlineData(6000, 9)] [InlineData(9999, 9)] [InlineData(10000, 10)] [InlineData(100000, 10)]
    public void UsesExactUserSuppliedBoundaries(long availablePoints, int expected)
        => Assert.Equal(expected, MembershipLevelRules.Calculate(availablePoints, Levels).Level);

    [Fact]
    public void ProgressIsRelativeToTheCurrentLevelAndReportsRemainingPoints()
    {
        var result = MembershipLevelRules.Calculate(1300, Levels);
        Assert.Equal(5, result.Level);
        Assert.Equal(6, result.NextLevel);
        Assert.Equal(1600, result.NextLevelMinPoints);
        Assert.Equal(300, result.PointsToNextLevel);
        Assert.Equal(50m, result.ProgressPercent);
    }

    [Fact]
    public void MaximumLevelHasNoNextThresholdAndIsFullyFilled()
    {
        var result = MembershipLevelRules.Calculate(10000, Levels);
        Assert.Null(result.NextLevel);
        Assert.Null(result.NextLevelMinPoints);
        Assert.Equal(0, result.PointsToNextLevel);
        Assert.Equal(100m, result.ProgressPercent);
    }

    [Fact]
    public void ReversingEarnedPointsCanLowerTheLevel()
    {
        Assert.Equal(6, MembershipLevelRules.Calculate(1600, Levels).Level);
        Assert.Equal(5, MembershipLevelRules.Calculate(1500, Levels).Level);
    }

    [Fact]
    public void AdminChangesTakeEffectWithoutHardcodedThresholds()
    {
        var levels = Levels.ToArray();
        levels[1] = levels[1] with { MinPoints = 200, Benefits = new[] { "Ưu đãi do admin cấu hình" } };
        Assert.Equal(1, MembershipLevelRules.Calculate(100, levels).Level);
        Assert.Single(MembershipLevelRules.Calculate(200, levels).Benefits);
    }

    [Fact]
    public void RejectsMissingLevelsDuplicateLevelsAndNonIncreasingThresholds()
    {
        Assert.Throws<BusinessException>(() => MembershipLevelRules.Validate(Levels.Take(9).ToArray()));
        var duplicate = Levels.ToArray(); duplicate[1] = duplicate[1] with { Level = 1 };
        Assert.Throws<BusinessException>(() => MembershipLevelRules.Validate(duplicate));
        var equal = Levels.ToArray(); equal[1] = equal[1] with { MinPoints = 0 };
        Assert.Throws<BusinessException>(() => MembershipLevelRules.Validate(equal));
        var missingZero = Levels.ToArray(); missingZero[0] = missingZero[0] with { MinPoints = 1 };
        Assert.Throws<BusinessException>(() => MembershipLevelRules.Validate(missingZero));
    }
}
