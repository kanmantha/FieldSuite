using FieldSuite.Domain.Common;

namespace FieldSuite.Web.ViewModels;

public class PermitFormViewModel
{
    public int ProjectId { get; set; }
    public PermitType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime RequestedStart { get; set; }
    public DateTime RequestedEnd { get; set; }
    public bool IsolationVerified { get; set; }
    public bool GasTestRequired { get; set; }
    public string SpecialConditions { get; set; } = string.Empty;
}

public static class PermitBadges
{
    public static string RiskBadge(RiskLevel level) => level switch
    {
        RiskLevel.Low => "success",
        RiskLevel.Medium => "info",
        RiskLevel.High => "warning",
        _ => "danger"
    };

    public static string StatusBadge(PermitStatus status) => status switch
    {
        PermitStatus.Draft => "secondary",
        PermitStatus.PendingApproval => "info",
        PermitStatus.Approved => "primary",
        PermitStatus.Active => "success",
        PermitStatus.Suspended => "warning",
        PermitStatus.Closed => "dark",
        PermitStatus.Rejected => "danger",
        PermitStatus.Expired => "secondary",
        _ => "secondary"
    };

    public static string DecisionBadge(ApprovalDecision decision) => decision switch
    {
        ApprovalDecision.Approved => "success",
        ApprovalDecision.Rejected => "danger",
        _ => "secondary"
    };
}