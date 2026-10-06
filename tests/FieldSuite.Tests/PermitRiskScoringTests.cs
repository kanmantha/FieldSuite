using FieldSuite.Domain.Common;
using FieldSuite.Domain.Entities;
using FieldSuite.Infrastructure.Services;

namespace FieldSuite.Tests;

public class PermitRiskScoringTests
{
    private readonly PermitRiskScoringService _service = new();

    private static WorkPermit Permit(PermitType type, int hours = 4, bool isolation = true, bool gas = false, string special = "") =>
        new()
        {
            Type = type,
            RequestedStart = new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc),
            RequestedEnd = new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc).AddHours(hours),
            IsolationVerified = isolation,
            GasTestRequired = gas,
            SpecialConditions = special
        };

    [Fact]
    public void SimpleLiftingPermit_IsLowRisk_SingleApprover()
    {
        var result = _service.Score(Permit(PermitType.Lifting));

        Assert.Equal(RiskLevel.Low, result.RiskLevel);
        Assert.Equal(1, result.Score);
        Assert.Equal(new[] { "SiteManager" }, result.RequiredApproverRoles);
    }

    [Fact]
    public void ConfinedSpaceWithoutIsolation_IsHighRisk_ThreeApprovers()
    {
        var result = _service.Score(Permit(PermitType.ConfinedSpace, isolation: false));

        Assert.Equal(5, result.Score);
        Assert.Equal(RiskLevel.High, result.RiskLevel);
        Assert.Equal(new[] { "SiteManager", "SafetyOfficer", "Admin" }, result.RequiredApproverRoles);
        Assert.Contains(result.Reasons, r => r.Contains("isolation not verified"));
    }

    [Fact]
    public void ConfinedSpaceWithIsolationAndGasTest_IsHighRisk()
    {
        var result = _service.Score(Permit(PermitType.ConfinedSpace, isolation: true, gas: true));

        Assert.Equal(4, result.Score);
        Assert.Equal(RiskLevel.High, result.RiskLevel);
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(12, 1)]
    [InlineData(30, 2)]
    public void DurationAddsRiskPoints(int hours, int expectedBonus)
    {
        var result = _service.Score(Permit(PermitType.Lifting, hours: hours));
        Assert.Equal(1 + expectedBonus, result.Score);
    }

    [Fact]
    public void HeightWorkAtNight_AddsOnePoint()
    {
        var result = _service.Score(Permit(PermitType.Height, special: "Night shift 22:00-05:00"));

        Assert.Equal(3, result.Score);
        Assert.Equal(RiskLevel.Medium, result.RiskLevel);
        Assert.Equal(new[] { "SiteManager", "SafetyOfficer" }, result.RequiredApproverRoles);
        Assert.Contains(result.Reasons, r => r.Contains("night"));
    }

    [Fact]
    public void ScoreIsAlwaysAccompaniedByExplanation()
    {
        var result = _service.Score(Permit(PermitType.HotWork, isolation: false, gas: true, hours: 30));

        Assert.Equal(RiskLevel.Extreme, result.RiskLevel);
        Assert.Contains(result.Reasons, r => r.Contains("Calculated score"));
        Assert.All(result.Reasons, r => Assert.False(string.IsNullOrWhiteSpace(r)));
    }
}
