using FieldSuite.Domain.Common;
using FieldSuite.Domain.Entities;

namespace FieldSuite.Infrastructure.Services;

public record RiskScoreResult(RiskLevel RiskLevel, int Score, IReadOnlyList<string> Reasons, IReadOnlyList<string> RequiredApproverRoles);

public interface IPermitRiskScoringService
{
    RiskScoreResult Score(WorkPermit permit);
}

public class PermitRiskScoringService : IPermitRiskScoringService
{
    private static readonly Dictionary<PermitType, int> BaseScores = new()
    {
        { PermitType.ConfinedSpace, 3 },
        { PermitType.HotWork, 2 },
        { PermitType.Height, 2 },
        { PermitType.Electrical, 2 },
        { PermitType.Excavation, 1 },
        { PermitType.Lifting, 1 }
    };

    public RiskScoreResult Score(WorkPermit permit)
    {
        var score = BaseScores[permit.Type];
        var reasons = new List<string> { $"Base risk for {permit.Type}: +{BaseScores[permit.Type]}" };

        bool isolationApplies = permit.Type is PermitType.Electrical or PermitType.ConfinedSpace or PermitType.HotWork;
        if (isolationApplies && !permit.IsolationVerified)
        {
            score += 2;
            reasons.Add("Energy/isolation not verified: +2");
        }
        else if (isolationApplies && permit.IsolationVerified)
        {
            reasons.Add("Energy/isolation verified: +0");
        }

        if (permit.GasTestRequired)
        {
            score += 1;
            reasons.Add("Atmosphere/gas testing required: +1");
        }

        var duration = permit.RequestedEnd - permit.RequestedStart;
        if (duration.TotalHours > 24)
        {
            score += 2;
            reasons.Add($"Long-duration permit ({duration.TotalHours:F0}h): +2");
        }
        else if (duration.TotalHours > 8)
        {
            score += 1;
            reasons.Add($"Extended permit ({duration.TotalHours:F0}h): +1");
        }

        if (permit.Type == PermitType.Height && permit.SpecialConditions.Contains("night", StringComparison.OrdinalIgnoreCase))
        {
            score += 1;
            reasons.Add("Work at height during night hours: +1");
        }

        var risk = score switch
        {
            <= 1 => RiskLevel.Low,
            <= 3 => RiskLevel.Medium,
            <= 5 => RiskLevel.High,
            _ => RiskLevel.Extreme
        };

        var roles = risk switch
        {
            RiskLevel.Low => new List<string> { "SiteManager" },
            RiskLevel.Medium => new List<string> { "SiteManager", "SafetyOfficer" },
            _ => new List<string> { "SiteManager", "SafetyOfficer", "Admin" }
        };

        reasons.Add($"Calculated score {score} → {risk}; requires {roles.Count}-level approval ({string.Join(" → ", roles)})");

        return new RiskScoreResult(risk, score, reasons, roles);
    }
}
