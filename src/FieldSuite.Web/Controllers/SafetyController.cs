using FieldSuite.Domain.Common;
using FieldSuite.Domain.Entities;
using FieldSuite.Infrastructure.Services;
using FieldSuite.Web.Services;
using FieldSuite.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FieldSuite.Web.Controllers;

[Authorize]
public class SafetyController : AppController
{
    private readonly INotificationService _notifications;

    private static readonly Dictionary<IncidentStatus, IncidentStatus> ForwardMap = new()
    {
        [IncidentStatus.Draft] = IncidentStatus.Reported,
        [IncidentStatus.Reported] = IncidentStatus.Investigating,
        [IncidentStatus.Investigating] = IncidentStatus.Resolved,
        [IncidentStatus.Resolved] = IncidentStatus.Closed
    };

    public SafetyController(AppDbContext db, ICurrentUser current, IAuditService audit,
        INotificationService notifications) : base(db, current, audit)
    {
        _notifications = notifications;
    }

    // ==================== INCIDENT LIST ====================

    public async Task<IActionResult> Index(IncidentStatus? status, int? projectId)
    {
        IQueryable<Incident> query = Db.Incidents
            .Where(i => i.OrganizationId == OrgId)
            .Include(i => i.Project)
            .Include(i => i.ReportedBy);

        if (status.HasValue) query = query.Where(i => i.Status == status.Value);
        if (projectId.HasValue) query = query.Where(i => i.ProjectId == projectId.Value);

        var incidents = await query.OrderByDescending(i => i.OccurredAt).ToListAsync();

        ViewBag.OpenCount = await Db.Incidents.CountAsync(i =>
            i.OrganizationId == OrgId && i.Status != IncidentStatus.Closed);
        ViewBag.InvestigatingCount = await Db.Incidents.CountAsync(i =>
            i.OrganizationId == OrgId && i.Status == IncidentStatus.Investigating);
        ViewBag.CriticalCount = await Db.Incidents.CountAsync(i =>
            i.OrganizationId == OrgId && i.Status != IncidentStatus.Closed && i.Severity == Severity.Critical);
        ViewBag.DraftCount = await Db.Incidents.CountAsync(i =>
            i.OrganizationId == OrgId && i.Status == IncidentStatus.Draft);
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentProjectId = projectId;
        ViewBag.Projects = await ProjectOptionsAsync(projectId);

        return View(incidents);
    }

    // ==================== REPORT INCIDENT ====================

    [HttpGet]
    public async Task<IActionResult> IncidentCreate()
    {
        ViewBag.Projects = await ProjectOptionsAsync();
        return View(new IncidentCreateVm { OccurredAt = DateTime.UtcNow });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IncidentCreate(IncidentCreateVm model, string? saveAsDraft)
    {
        if (model.ProjectId <= 0 ||
            !await Db.Projects.AnyAsync(p => p.Id == model.ProjectId && p.OrganizationId == OrgId))
            ModelState.AddModelError(nameof(model.ProjectId), "Select a valid project.");
        if (string.IsNullOrWhiteSpace(model.Title))
            ModelState.AddModelError(nameof(model.Title), "Title is required.");
        if (string.IsNullOrWhiteSpace(model.Location))
            ModelState.AddModelError(nameof(model.Location), "Location is required.");

        if (!ModelState.IsValid)
        {
            ViewBag.Projects = await ProjectOptionsAsync(model.ProjectId);
            return View(model);
        }

        var isDraft = string.Equals(saveAsDraft, "draft", StringComparison.OrdinalIgnoreCase);

        var incident = new Incident
        {
            OrganizationId = OrgId,
            ProjectId = model.ProjectId,
            Reference = "TMP-" + Guid.NewGuid().ToString("N")[..24],
            Type = model.Type,
            Severity = model.Severity,
            Title = model.Title.Trim(),
            Description = model.Description ?? string.Empty,
            OccurredAt = model.OccurredAt,
            Location = model.Location.Trim(),
            RootCause = model.RootCause ?? string.Empty,
            ReportedById = Current.UserId,
            Status = isDraft ? IncidentStatus.Draft : IncidentStatus.Reported
        };

        Db.Incidents.Add(incident);
        await Db.SaveChangesAsync();

        incident.Reference = $"SIN-{incident.Id:D4}";
        await Db.SaveChangesAsync();

        await AuditAsync("Create", "Incident", incident.Id,
            $"Created incident {incident.Reference} ({incident.Type}, {incident.Severity}) — {incident.Title}");

        var link = Url.Action(nameof(IncidentDetails), "Safety", new { id = incident.Id }) ?? "#";
        if (incident.Status == IncidentStatus.Reported)
        {
            await _notifications.NotifyRoleAsync(OrgId, "SafetyOfficer",
                $"Incident {incident.Reference} reported ({incident.Severity}): {incident.Title} at {incident.Location}.",
                link);
        }

        TempData["Success"] = incident.Status == IncidentStatus.Draft
            ? $"Incident {incident.Reference} saved as a draft."
            : $"Incident {incident.Reference} reported and sent to the Safety Officer.";
        return RedirectToAction(nameof(IncidentDetails), new { id = incident.Id });
    }

    // ==================== INCIDENT DETAILS ====================

    public async Task<IActionResult> IncidentDetails(int id)
    {
        var incident = await Db.Incidents
            .Include(i => i.Project)
            .Include(i => i.ReportedBy)
            .Include(i => i.ReviewedBy)
            .Include(i => i.CorrectiveActions).ThenInclude(c => c.AssignedTo)
            .FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == OrgId);
        if (incident is null) return NotFound();

        return View(new IncidentDetailsVm
        {
            Incident = incident,
            AssignedToList = await UserOptionsAsync()
        });
    }

    // ==================== INCIDENT EDIT ====================

    [HttpGet]
    public async Task<IActionResult> IncidentEdit(int id)
    {
        var incident = await Db.Incidents.FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == OrgId);
        if (incident is null) return NotFound();

        if (incident.Status != IncidentStatus.Draft && incident.Status != IncidentStatus.Reported)
        {
            TempData["Error"] = "Only draft or reported incidents can be edited.";
            return RedirectToAction(nameof(IncidentDetails), new { id });
        }

        ViewBag.Projects = await ProjectOptionsAsync(incident.ProjectId);
        return View(new IncidentEditVm
        {
            Id = incident.Id,
            ProjectId = incident.ProjectId,
            Type = incident.Type,
            Severity = incident.Severity,
            Title = incident.Title,
            Description = incident.Description,
            OccurredAt = incident.OccurredAt,
            Location = incident.Location,
            RootCause = incident.RootCause
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IncidentEdit(int id, IncidentEditVm model)
    {
        var incident = await Db.Incidents.FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == OrgId);
        if (incident is null) return NotFound();

        if (incident.Status != IncidentStatus.Draft && incident.Status != IncidentStatus.Reported)
        {
            TempData["Error"] = "Only draft or reported incidents can be edited.";
            return RedirectToAction(nameof(IncidentDetails), new { id });
        }

        if (model.ProjectId <= 0 ||
            !await Db.Projects.AnyAsync(p => p.Id == model.ProjectId && p.OrganizationId == OrgId))
            ModelState.AddModelError(nameof(model.ProjectId), "Select a valid project.");
        if (string.IsNullOrWhiteSpace(model.Title))
            ModelState.AddModelError(nameof(model.Title), "Title is required.");
        if (string.IsNullOrWhiteSpace(model.Location))
            ModelState.AddModelError(nameof(model.Location), "Location is required.");

        if (!ModelState.IsValid)
        {
            model.Id = incident.Id;
            ViewBag.Projects = await ProjectOptionsAsync(model.ProjectId);
            return View(model);
        }

        incident.ProjectId = model.ProjectId;
        incident.Type = model.Type;
        incident.Severity = model.Severity;
        incident.Title = model.Title.Trim();
        incident.Description = model.Description ?? string.Empty;
        incident.OccurredAt = model.OccurredAt;
        incident.Location = model.Location.Trim();
        incident.RootCause = model.RootCause ?? string.Empty;

        await Db.SaveChangesAsync();
        await AuditAsync("Update", "Incident", incident.Id, $"Updated incident {incident.Reference}");
        TempData["Success"] = $"{incident.Reference} updated.";
        return RedirectToAction(nameof(IncidentDetails), new { id });
    }

    // ==================== STATUS TRANSITION ====================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SafetyOfficer,SiteManager,Admin")]
    public async Task<IActionResult> IncidentTransition(int id, IncidentStatus to)
    {
        var incident = await Db.Incidents.FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == OrgId);
        if (incident is null) return NotFound();

        var from = incident.Status;
        if (!ForwardMap.TryGetValue(from, out var expected) || expected != to)
        {
            var next = ForwardMap.GetValueOrDefault(from, from);
            TempData["Error"] = from == IncidentStatus.Closed
                ? $"{incident.Reference} is closed and can no longer change status."
                : $"Incidents only move forward — {from} can go to {next} next.";
            return RedirectToAction(nameof(IncidentDetails), new { id });
        }

        incident.Status = to;
        if (to == IncidentStatus.Closed) incident.ReviewedById = Current.UserId;

        await Db.SaveChangesAsync();
        await AuditAsync("Transition", "Incident", incident.Id,
            $"{incident.Reference} moved {from} → {to}" + (to == IncidentStatus.Closed ? $" (reviewed by {Current.FullName})" : string.Empty));

        await _notifications.NotifyAsync(OrgId, incident.ReportedById,
            $"Incident {incident.Reference} ({incident.Title}) moved from {from} to {to}.",
            Url.Action(nameof(IncidentDetails), "Safety", new { id = incident.Id }) ?? "#");

        TempData["Success"] = $"{incident.Reference} moved to {to}.";
        return RedirectToAction(nameof(IncidentDetails), new { id });
    }

    // ==================== CORRECTIVE ACTIONS ====================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SafetyOfficer,SiteManager,Admin")]
    public async Task<IActionResult> CorrectiveActionAdd(int incidentId, string description, string? assignedToId, DateTime dueDate)
    {
        var incident = await Db.Incidents.FirstOrDefaultAsync(i => i.Id == incidentId && i.OrganizationId == OrgId);
        if (incident is null) return NotFound();

        if (string.IsNullOrWhiteSpace(description))
        {
            TempData["Error"] = "Describe the corrective action before adding it.";
            return RedirectToAction(nameof(IncidentDetails), new { id = incidentId });
        }

        if (dueDate == default)
        {
            TempData["Error"] = "A due date is required for the corrective action.";
            return RedirectToAction(nameof(IncidentDetails), new { id = incidentId });
        }

        if (!string.IsNullOrWhiteSpace(assignedToId) &&
            !await Db.Users.AnyAsync(u => u.Id == assignedToId && u.OrganizationId == OrgId))
        {
            TempData["Error"] = "Select a valid assignee.";
            return RedirectToAction(nameof(IncidentDetails), new { id = incidentId });
        }

        var action = new CorrectiveAction
        {
            IncidentId = incidentId,
            Description = description.Trim(),
            AssignedToId = string.IsNullOrWhiteSpace(assignedToId) ? null : assignedToId.Trim(),
            DueDate = dueDate.Date,
            Status = CorrectiveActionStatus.Open
        };

        Db.CorrectiveActions.Add(action);
        await Db.SaveChangesAsync();

        await AuditAsync("Create", "CorrectiveAction", action.Id,
            $"Added corrective action on {incident.Reference}: {action.Description} (due {action.DueDate:dd MMM yyyy})");

        if (!string.IsNullOrWhiteSpace(action.AssignedToId))
        {
            await _notifications.NotifyAsync(OrgId, action.AssignedToId,
                $"Corrective action on {incident.Reference}: {action.Description} — due {action.DueDate:dd MMM yyyy}.",
                Url.Action(nameof(IncidentDetails), "Safety", new { id = incidentId }) ?? "#");
        }

        TempData["Success"] = "Corrective action added.";
        return RedirectToAction(nameof(IncidentDetails), new { id = incidentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SafetyOfficer,SiteManager,Admin")]
    public async Task<IActionResult> CorrectiveActionUpdate(int id, CorrectiveActionStatus status)
    {
        var action = await Db.CorrectiveActions
            .Include(a => a.Incident)
            .FirstOrDefaultAsync(a => a.Id == id && a.Incident!.OrganizationId == OrgId);
        if (action is null) return NotFound();

        var from = action.Status;
        action.Status = status;
        await Db.SaveChangesAsync();

        await AuditAsync("Update", "CorrectiveAction", action.Id,
            $"Corrective action '{action.Description}' on {action.Incident!.Reference} moved {from} → {status}");

        TempData["Success"] = $"Corrective action marked {status}.";
        return RedirectToAction(nameof(IncidentDetails), new { id = action.IncidentId });
    }

    // ==================== TOOLBOX TALKS ====================

    public async Task<IActionResult> ToolboxTalks(int? projectId)
    {
        IQueryable<ToolboxTalk> query = Db.ToolboxTalks
            .Where(t => t.Project!.OrganizationId == OrgId)
            .Include(t => t.Project)
            .Include(t => t.Facilitator)
            .Include(t => t.Attendances);

        if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);

        var talks = await query.OrderByDescending(t => t.TalkDate).ToListAsync();

        ViewBag.TalkCount = await Db.ToolboxTalks.CountAsync(t => t.Project!.OrganizationId == OrgId);
        ViewBag.RecentCount = await Db.ToolboxTalks.CountAsync(t =>
            t.Project!.OrganizationId == OrgId && t.TalkDate >= DateTime.UtcNow.AddDays(-30));
        ViewBag.AttendanceCount = await Db.ToolboxAttendances.CountAsync(a =>
            a.ToolboxTalk!.Project!.OrganizationId == OrgId);
        ViewBag.CurrentProjectId = projectId;
        ViewBag.Projects = await ProjectOptionsAsync(projectId);

        return View(talks);
    }

    [HttpGet]
    public async Task<IActionResult> ToolboxTalkCreate()
    {
        return View(new ToolboxTalkCreateVm
        {
            TalkDate = DateTime.UtcNow,
            Projects = await ProjectOptionsAsync(),
            Workers = await WorkerOptionsAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToolboxTalkCreate(ToolboxTalkCreateVm model)
    {
        if (model.ProjectId <= 0 ||
            !await Db.Projects.AnyAsync(p => p.Id == model.ProjectId && p.OrganizationId == OrgId))
            ModelState.AddModelError(nameof(model.ProjectId), "Select a valid project.");
        if (string.IsNullOrWhiteSpace(model.Topic))
            ModelState.AddModelError(nameof(model.Topic), "Topic is required.");
        if (model.DurationMinutes <= 0)
            ModelState.AddModelError(nameof(model.DurationMinutes), "Duration must be at least 1 minute.");

        if (!ModelState.IsValid)
        {
            model.Projects = await ProjectOptionsAsync(model.ProjectId);
            model.Workers = await WorkerOptionsAsync();
            return View(model);
        }

        var talk = new ToolboxTalk
        {
            ProjectId = model.ProjectId,
            Topic = model.Topic.Trim(),
            TalkDate = model.TalkDate,
            DurationMinutes = model.DurationMinutes,
            FacilitatorId = Current.UserId,
            Notes = model.Notes ?? string.Empty
        };

        Db.ToolboxTalks.Add(talk);
        await Db.SaveChangesAsync();

        var requestedIds = (model.WorkerIds ?? new List<int>()).Distinct().ToList();
        var validIds = requestedIds.Count == 0
            ? new List<int>()
            : await Db.Workers
                .Where(w => w.OrganizationId == OrgId && requestedIds.Contains(w.Id))
                .Select(w => w.Id)
                .ToListAsync();

        foreach (var workerId in validIds)
        {
            Db.ToolboxAttendances.Add(new ToolboxAttendance
            {
                ToolboxTalkId = talk.Id,
                WorkerId = workerId
            });
        }
        await Db.SaveChangesAsync();

        await AuditAsync("Create", "ToolboxTalk", talk.Id,
            $"Toolbox talk '{talk.Topic}' on {talk.TalkDate:dd MMM yyyy} — {validIds.Count} attendee(s)");
        TempData["Success"] = validIds.Count > 0
            ? $"Toolbox talk '{talk.Topic}' recorded with {validIds.Count} attendee(s)."
            : $"Toolbox talk '{talk.Topic}' recorded (no workers selected).";
        return RedirectToAction(nameof(ToolboxTalkDetails), new { id = talk.Id });
    }

    public async Task<IActionResult> ToolboxTalkDetails(int id)
    {
        var talk = await Db.ToolboxTalks
            .Include(t => t.Project)
            .Include(t => t.Facilitator)
            .Include(t => t.Attendances).ThenInclude(a => a.Worker)
            .FirstOrDefaultAsync(t => t.Id == id && t.Project!.OrganizationId == OrgId);
        if (talk is null) return NotFound();

        return View(talk);
    }

    // ==================== HELPERS ====================

    private async Task<SelectList> ProjectOptionsAsync(int? selectedId = null)
    {
        var projects = await Db.Projects
            .Where(p => p.OrganizationId == OrgId)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync();
        return new SelectList(projects, "Id", "Name", selectedId);
    }

    private async Task<List<SelectListItem>> WorkerOptionsAsync()
    {
        var workers = await Db.Workers
            .Where(w => w.OrganizationId == OrgId && w.IsActive)
            .OrderBy(w => w.FullName)
            .Select(w => new { w.Id, w.FullName })
            .ToListAsync();

        return workers
            .Select(w => new SelectListItem(w.FullName, w.Id.ToString()))
            .ToList();
    }

    private async Task<SelectList> UserOptionsAsync(string? selectedId = null)
    {
        var users = await Db.Users
            .Where(u => u.OrganizationId == OrgId)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync();
        return new SelectList(users, "Id", "FullName", selectedId);
    }
}
