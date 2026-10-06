using FieldSuite.Domain.Common;

namespace FieldSuite.Domain.Entities;

public class Incident : Entity
{
    public int OrganizationId { get; set; }
    public int ProjectId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public IncidentType Type { get; set; }
    public Severity Severity { get; set; } = Severity.Low;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string Location { get; set; } = string.Empty;
    public string RootCause { get; set; } = string.Empty;
    public string ReportedById { get; set; } = string.Empty;
    public IncidentStatus Status { get; set; } = IncidentStatus.Draft;
    public string? ReviewedById { get; set; }

    public Project? Project { get; set; }
    public AppUser? ReportedBy { get; set; }
    public AppUser? ReviewedBy { get; set; }
    public ICollection<CorrectiveAction> CorrectiveActions { get; set; } = new List<CorrectiveAction>();
}

public class CorrectiveAction : Entity
{
    public int IncidentId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? AssignedToId { get; set; }
    public DateTime DueDate { get; set; }
    public CorrectiveActionStatus Status { get; set; } = CorrectiveActionStatus.Open;

    public Incident? Incident { get; set; }
    public AppUser? AssignedTo { get; set; }
}

public class ToolboxTalk : Entity
{
    public int ProjectId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public DateTime TalkDate { get; set; } = DateTime.UtcNow;
    public int DurationMinutes { get; set; }
    public string FacilitatorId { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public Project? Project { get; set; }
    public AppUser? Facilitator { get; set; }
    public ICollection<ToolboxAttendance> Attendances { get; set; } = new List<ToolboxAttendance>();
}

public class ToolboxAttendance : Entity
{
    public int ToolboxTalkId { get; set; }
    public int WorkerId { get; set; }

    public ToolboxTalk? ToolboxTalk { get; set; }
    public Worker? Worker { get; set; }
}
