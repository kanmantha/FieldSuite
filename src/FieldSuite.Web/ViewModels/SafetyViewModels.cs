using System.ComponentModel.DataAnnotations;
using FieldSuite.Domain.Common;
using FieldSuite.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FieldSuite.Web.ViewModels;

public class IncidentCreateVm
{
    [Required(ErrorMessage = "Select a project.")]
    public int ProjectId { get; set; }

    public IncidentType Type { get; set; } = IncidentType.Incident;
    public Severity Severity { get; set; } = Severity.Low;

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, ErrorMessage = "Title must be 200 characters or fewer.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Location is required.")]
    [StringLength(200)]
    public string Location { get; set; } = string.Empty;

    [StringLength(2000)]
    public string RootCause { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public SelectList? Projects { get; set; }
}

public class IncidentEditVm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Select a project.")]
    public int ProjectId { get; set; }

    public IncidentType Type { get; set; }
    public Severity Severity { get; set; } = Severity.Low;

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, ErrorMessage = "Title must be 200 characters or fewer.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Location is required.")]
    [StringLength(200)]
    public string Location { get; set; } = string.Empty;

    [StringLength(2000)]
    public string RootCause { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public SelectList? Projects { get; set; }
}

public class IncidentDetailsVm
{
    public Incident Incident { get; set; } = new();
    public SelectList AssignedToList { get; set; } = new(new List<SelectListItem>());
}

public class ToolboxTalkCreateVm
{
    [Required(ErrorMessage = "Select a project.")]
    public int ProjectId { get; set; }

    [Required(ErrorMessage = "Topic is required.")]
    [StringLength(200, ErrorMessage = "Topic must be 200 characters or fewer.")]
    public string Topic { get; set; } = string.Empty;

    public DateTime TalkDate { get; set; } = DateTime.UtcNow;

    [Range(1, 600, ErrorMessage = "Duration must be between 1 and 600 minutes.")]
    public int DurationMinutes { get; set; } = 15;

    [StringLength(2000)]
    public string Notes { get; set; } = string.Empty;

    public List<int> WorkerIds { get; set; } = new();

    public SelectList? Projects { get; set; }
    public List<SelectListItem>? Workers { get; set; }
}

public static class SafetyBadges
{
    public static string SeverityBadge(Severity severity) => severity switch
    {
        Severity.Low => "success",
        Severity.Medium => "info",
        Severity.High => "warning",
        _ => "danger"
    };

    public static string StatusBadge(IncidentStatus status) => status switch
    {
        IncidentStatus.Draft => "secondary",
        IncidentStatus.Reported => "info",
        IncidentStatus.Investigating => "warning",
        IncidentStatus.Resolved => "primary",
        _ => "dark"
    };

    public static string TypeBadge(IncidentType type) => type switch
    {
        IncidentType.NearMiss => "info",
        IncidentType.FirstAid => "primary",
        _ => "secondary"
    };

    public static string ActionBadge(CorrectiveActionStatus status) => status switch
    {
        CorrectiveActionStatus.Open => "secondary",
        CorrectiveActionStatus.InProgress => "info",
        CorrectiveActionStatus.Done => "success",
        _ => "danger"
    };
}
