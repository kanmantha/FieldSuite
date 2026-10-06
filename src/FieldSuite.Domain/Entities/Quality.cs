using FieldSuite.Domain.Common;

namespace FieldSuite.Domain.Entities;

public class Inspection : Entity
{
    public int ProjectId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public InspectionType Type { get; set; } = InspectionType.Internal;
    public string Title { get; set; } = string.Empty;
    public DateTime InspectionDate { get; set; } = DateTime.UtcNow;
    public string InspectorId { get; set; } = string.Empty;
    public InspectionStatus Status { get; set; } = InspectionStatus.Draft;
    public string OverallNotes { get; set; } = string.Empty;

    public Project? Project { get; set; }
    public AppUser? Inspector { get; set; }
    public ICollection<InspectionItem> Items { get; set; } = new List<InspectionItem>();
}

public class InspectionItem : Entity
{
    public int InspectionId { get; set; }
    public string ChecklistText { get; set; } = string.Empty;
    public CheckResult Result { get; set; } = CheckResult.Pass;
    public string Comment { get; set; } = string.Empty;

    public Inspection? Inspection { get; set; }
}

public class Snag : Entity
{
    public int ProjectId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public SnagPriority Priority { get; set; } = SnagPriority.Medium;
    public string RaisedById { get; set; } = string.Empty;
    public string? AssignedToId { get; set; }
    public DateTime? DueDate { get; set; }
    public string? PhotoPath { get; set; }
    public SnagStatus Status { get; set; } = SnagStatus.Open;
    public DateTime? ClosedAt { get; set; }

    public Project? Project { get; set; }
    public AppUser? RaisedBy { get; set; }
    public AppUser? AssignedTo { get; set; }
}

public class NCR : Entity
{
    public int ProjectId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Severity Severity { get; set; } = Severity.Medium;
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedDate { get; set; } = DateTime.UtcNow;
    public string ContainmentAction { get; set; } = string.Empty;
    public NCRStatus Status { get; set; } = NCRStatus.Open;

    public Project? Project { get; set; }
}
