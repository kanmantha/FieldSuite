using System.ComponentModel.DataAnnotations;
using FieldSuite.Domain.Common;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FieldSuite.Web.ViewModels;

public class SnagCreateVm
{
    [Required, Display(Name = "Project")] public int ProjectId { get; set; }
    [Required, MaxLength(300)] public string Title { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Description { get; set; } = string.Empty;
    [Required, MaxLength(300)] public string Location { get; set; } = string.Empty;
    public SnagPriority Priority { get; set; } = SnagPriority.Medium;
    [Display(Name = "Assign to")] public string? AssignedToId { get; set; }
    [Display(Name = "Due date")] public DateTime? DueDate { get; set; }
    public IFormFile? Photo { get; set; }
    public SelectList? Projects { get; set; }
    public SelectList? Users { get; set; }
}

public class SnagEditVm
{
    public int Id { get; set; }
    [Required, Display(Name = "Project")] public int ProjectId { get; set; }
    [Required, MaxLength(300)] public string Title { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Description { get; set; } = string.Empty;
    [Required, MaxLength(300)] public string Location { get; set; } = string.Empty;
    public SnagPriority Priority { get; set; }
    [Display(Name = "Assign to")] public string? AssignedToId { get; set; }
    [Display(Name = "Due date")] public DateTime? DueDate { get; set; }
    public SelectList? Projects { get; set; }
    public SelectList? Users { get; set; }
}

public class InspectionCreateVm
{
    [Required, Display(Name = "Project")] public int ProjectId { get; set; }
    public InspectionType Type { get; set; } = InspectionType.Internal;
    [Required, MaxLength(300)] public string Title { get; set; } = string.Empty;
    [Display(Name = "Inspection date")] public DateTime InspectionDate { get; set; } = DateTime.UtcNow.Date;
    public SelectList? Projects { get; set; }
}

public class NcrCreateVm
{
    [Required, Display(Name = "Project")] public int ProjectId { get; set; }
    [Required, MaxLength(300)] public string Title { get; set; } = string.Empty;
    public Severity Severity { get; set; } = Severity.Medium;
    [Required, MaxLength(2000)] public string Description { get; set; } = string.Empty;
    [Display(Name = "Detected date")] public DateTime DetectedDate { get; set; } = DateTime.UtcNow.Date;
    [Display(Name = "Containment action")] public string ContainmentAction { get; set; } = string.Empty;
    public SelectList? Projects { get; set; }
}
