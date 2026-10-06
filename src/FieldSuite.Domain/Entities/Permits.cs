using FieldSuite.Domain.Common;

namespace FieldSuite.Domain.Entities;

public class WorkPermit : Entity
{
    public int OrganizationId { get; set; }
    public int ProjectId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public PermitType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string RequestedById { get; set; } = string.Empty;
    public DateTime RequestedStart { get; set; }
    public DateTime RequestedEnd { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public PermitStatus Status { get; set; } = PermitStatus.Draft;
    public bool IsolationVerified { get; set; }
    public bool GasTestRequired { get; set; }
    public string SpecialConditions { get; set; } = string.Empty;
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }

    public Project? Project { get; set; }
    public AppUser? RequestedBy { get; set; }
    public ICollection<PermitChecklistItem> ChecklistItems { get; set; } = new List<PermitChecklistItem>();
    public ICollection<PermitApproval> Approvals { get; set; } = new List<PermitApproval>();
}

public class PermitChecklistItem : Entity
{
    public int WorkPermitId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsChecked { get; set; }
    public string? CheckedById { get; set; }
    public DateTime? CheckedAt { get; set; }

    public WorkPermit? WorkPermit { get; set; }
    public AppUser? CheckedBy { get; set; }
}

public class PermitApproval : Entity
{
    public int WorkPermitId { get; set; }
    public int Level { get; set; }
    public string ApproverRole { get; set; } = string.Empty;
    public string? ApproverId { get; set; }
    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Pending;
    public string Comment { get; set; } = string.Empty;
    public DateTime? DecidedAt { get; set; }

    public WorkPermit? WorkPermit { get; set; }
    public AppUser? Approver { get; set; }
}
