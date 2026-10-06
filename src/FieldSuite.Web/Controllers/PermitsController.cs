using FieldSuite.Domain.Common;
using FieldSuite.Domain.Entities;
using FieldSuite.Infrastructure.Services;
using FieldSuite.Web.Services;
using FieldSuite.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FieldSuite.Web.Controllers;

[Authorize]
public class PermitsController : AppController
{
    private readonly IPermitRiskScoringService _risk;
    private readonly INotificationService _notifications;

    private static readonly Dictionary<PermitType, string[]> ChecklistTemplates = new()
    {
        [PermitType.HotWork] = new[]
        {
            "Area cleared of combustibles within 11 m",
            "Fire extinguishers staged at the work face",
            "Gas test completed and recorded",
            "Fire watch briefed and standing by",
            "Adequate ventilation confirmed",
            "Gas cylinders removed / isolated on completion"
        },
        [PermitType.ConfinedSpace] = new[]
        {
            "Isolation / LOTO applied, verified and signed",
            "Atmospheric test (O2 / LEL / toxics) recorded",
            "Attendant stationed at the entry point",
            "Retrieval / rescue equipment rigged and inspected",
            "Entry roster updated with all entrants",
            "Forced ventilation / blower running and monitored"
        },
        [PermitType.Height] = new[]
        {
            "Scaffold / MEWP handover tag verified as green",
            "Harness and double lanyard inspected",
            "Exclusion zone barricaded below work area",
            "Dropped-object protection in place",
            "Weather check — wind speed within 40 km/h limit",
            "Rescue plan briefed and rescue kit available"
        },
        [PermitType.Electrical] = new[]
        {
            "LOTO applied and circuit proven dead",
            "Circuit identified and isolated",
            "Arc-flash PPE issued and worn",
            "Earth bonds applied and verified",
            "Competent person sign-off obtained",
            "Barriers / covers fitted on live parts"
        },
        [PermitType.Excavation] = new[]
        {
            "Underground services surveyed and located",
            "Competent person inspection completed",
            "Edge protection installed around opening",
            "Safe access / egress ladder provided",
            "Spoil stored at least 0.5 m from the edge",
            "Dewatering arrangements in place"
        },
        [PermitType.Lifting] = new[]
        {
            "Lift plan approved for the task",
            "Crane certificate current and valid",
            "Operator and rigger certified for the task",
            "Exclusion zone set up and briefed",
            "Communication method agreed (radio / hand signals)",
            "Wind speed checked against crane limits"
        }
    };

    public PermitsController(AppDbContext db, ICurrentUser current, IAuditService audit,
        IPermitRiskScoringService risk, INotificationService notifications)
        : base(db, current, audit)
    {
        _risk = risk;
        _notifications = notifications;
    }

    public async Task<IActionResult> Index(PermitStatus? status, PermitType? type)
    {
        IQueryable<WorkPermit> query = Db.WorkPermits
            .Where(w => w.OrganizationId == OrgId)
            .Include(w => w.Project)
            .Include(w => w.RequestedBy);

        if (status.HasValue) query = query.Where(w => w.Status == status.Value);
        if (type.HasValue) query = query.Where(w => w.Type == type.Value);

        var permits = await query.OrderByDescending(w => w.RequestedStart).ToListAsync();

        ViewBag.ActiveCount = await Db.WorkPermits.CountAsync(w => w.OrganizationId == OrgId && w.Status == PermitStatus.Active);
        ViewBag.PendingCount = await Db.WorkPermits.CountAsync(w => w.OrganizationId == OrgId && w.Status == PermitStatus.PendingApproval);
        ViewBag.ExpiringSoonCount = await Db.WorkPermits.CountAsync(w =>
            w.OrganizationId == OrgId && w.Status == PermitStatus.Active && w.RequestedEnd < DateTime.UtcNow.AddHours(24));
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentType = type;

        return View(permits);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Projects = await ProjectOptionsAsync();
        return View(new PermitFormViewModel
        {
            RequestedStart = DateTime.UtcNow,
            RequestedEnd = DateTime.UtcNow.AddHours(8)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PermitFormViewModel model)
    {
        await ValidateFormAsync(model);

        if (!ModelState.IsValid)
        {
            ViewBag.Projects = await ProjectOptionsAsync();
            return View(model);
        }

        var permit = new WorkPermit
        {
            OrganizationId = OrgId,
            ProjectId = model.ProjectId,
            Reference = "TMP-" + Guid.NewGuid().ToString("N")[..24],
            Type = model.Type,
            Title = model.Title.Trim(),
            Description = model.Description ?? string.Empty,
            Location = model.Location.Trim(),
            RequestedById = Current.UserId,
            RequestedStart = model.RequestedStart,
            RequestedEnd = model.RequestedEnd,
            IsolationVerified = model.IsolationVerified,
            GasTestRequired = model.GasTestRequired,
            SpecialConditions = model.SpecialConditions ?? string.Empty,
            Status = PermitStatus.Draft
        };

        var score = _risk.Score(permit);
        permit.RiskLevel = score.RiskLevel;

        var level = 1;
        foreach (var role in score.RequiredApproverRoles)
        {
            permit.Approvals.Add(new PermitApproval
            {
                Level = level++,
                ApproverRole = role,
                Decision = ApprovalDecision.Pending
            });
        }

        foreach (var text in ChecklistTemplates[permit.Type])
        {
            permit.ChecklistItems.Add(new PermitChecklistItem { Text = text });
        }

        Db.WorkPermits.Add(permit);
        await Db.SaveChangesAsync();

        permit.Reference = $"PWT-{permit.Id:D4}";
        await Db.SaveChangesAsync();

        await AuditAsync("Create", "WorkPermit", permit.Id,
            $"Created permit {permit.Reference} ({permit.Type}, {score.Score} pts → {score.RiskLevel})");
        TempData["Success"] = $"Permit {permit.Reference} created as draft. Submit it when ready.";
        return RedirectToAction(nameof(Details), new { id = permit.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var permit = await LoadPermitAsync(id);
        if (permit is null) return NotFound();

        var score = _risk.Score(permit);
        ViewBag.Score = score;
        ViewBag.AsAt = DateTime.UtcNow;

        var nextPending = permit.Approvals
            .Where(a => a.Decision == ApprovalDecision.Pending)
            .OrderBy(a => a.Level)
            .FirstOrDefault();
        ViewBag.NextApproval = nextPending;
        ViewBag.CanDecide = nextPending is not null
            && permit.Status == PermitStatus.PendingApproval
            && Current.IsInRole(nextPending.ApproverRole);
        ViewBag.ChecklistEnabled = permit.Status != PermitStatus.Closed && permit.Status != PermitStatus.Expired;

        return View(permit);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var permit = await Db.WorkPermits.FirstOrDefaultAsync(w => w.Id == id && w.OrganizationId == OrgId);
        if (permit is null) return NotFound();

        if (permit.Status != PermitStatus.Draft && permit.Status != PermitStatus.PendingApproval)
        {
            TempData["Error"] = "Only draft or pending-approval permits can be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        ViewBag.PermitId = permit.Id;
        ViewBag.Projects = await ProjectOptionsAsync(permit.ProjectId);
        return View(new PermitFormViewModel
        {
            ProjectId = permit.ProjectId,
            Type = permit.Type,
            Title = permit.Title,
            Description = permit.Description,
            Location = permit.Location,
            RequestedStart = permit.RequestedStart,
            RequestedEnd = permit.RequestedEnd,
            IsolationVerified = permit.IsolationVerified,
            GasTestRequired = permit.GasTestRequired,
            SpecialConditions = permit.SpecialConditions
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PermitFormViewModel model)
    {
        var permit = await Db.WorkPermits
            .Include(w => w.Approvals)
            .FirstOrDefaultAsync(w => w.Id == id && w.OrganizationId == OrgId);
        if (permit is null) return NotFound();
        ViewBag.PermitId = permit.Id;

        if (permit.Status != PermitStatus.Draft && permit.Status != PermitStatus.PendingApproval)
        {
            TempData["Error"] = "Only draft or pending-approval permits can be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await ValidateFormAsync(model);

        if (!ModelState.IsValid)
        {
            ViewBag.Projects = await ProjectOptionsAsync(model.ProjectId);
            return View(model);
        }

        permit.ProjectId = model.ProjectId;
        permit.Title = model.Title.Trim();
        permit.Description = model.Description ?? string.Empty;
        permit.Location = model.Location.Trim();
        permit.RequestedStart = model.RequestedStart;
        permit.RequestedEnd = model.RequestedEnd;
        permit.IsolationVerified = model.IsolationVerified;
        permit.GasTestRequired = model.GasTestRequired;
        permit.SpecialConditions = model.SpecialConditions ?? string.Empty;

        var score = _risk.Score(permit);

        if (permit.Status == PermitStatus.Draft && score.RiskLevel != permit.RiskLevel)
        {
            permit.RiskLevel = score.RiskLevel;
            Db.PermitApprovals.RemoveRange(permit.Approvals);
            permit.Approvals.Clear();
            var level = 1;
            foreach (var role in score.RequiredApproverRoles)
            {
                permit.Approvals.Add(new PermitApproval
                {
                    WorkPermitId = permit.Id,
                    Level = level++,
                    ApproverRole = role,
                    Decision = ApprovalDecision.Pending
                });
            }
        }

        await Db.SaveChangesAsync();
        await AuditAsync("Update", "WorkPermit", permit.Id, $"Updated permit {permit.Reference}");
        TempData["Success"] = "Permit updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChecklistToggle(int id, int itemId)
    {
        var permit = await Db.WorkPermits.FirstOrDefaultAsync(w => w.Id == id && w.OrganizationId == OrgId);
        if (permit is null) return NotFound();

        if (permit.Status == PermitStatus.Closed || permit.Status == PermitStatus.Expired)
        {
            TempData["Error"] = "The checklist is locked for closed or expired permits.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var item = await Db.PermitChecklistItems.FirstOrDefaultAsync(c => c.Id == itemId && c.WorkPermitId == id);
        if (item is null) return NotFound();

        item.IsChecked = !item.IsChecked;
        item.CheckedById = item.IsChecked ? Current.UserId : null;
        item.CheckedAt = item.IsChecked ? DateTime.UtcNow : null;
        await Db.SaveChangesAsync();
        await AuditAsync("ChecklistToggle", "PermitChecklistItem", item.Id,
            item.IsChecked ? $"Checked '{item.Text}' on permit {permit.Reference}" : $"Unchecked '{item.Text}' on permit {permit.Reference}");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,SafetyOfficer,Admin")]
    public async Task<IActionResult> Approve(int id, int approvalId, string? comment)
    {
        var permit = await LoadPermitAsync(id);
        if (permit is null) return NotFound();

        if (permit.Status != PermitStatus.PendingApproval)
        {
            TempData["Error"] = "This permit is not awaiting approval.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var approval = permit.Approvals.FirstOrDefault(a => a.Id == approvalId);
        if (approval is null) return NotFound();

        var nextPending = permit.Approvals
            .Where(a => a.Decision == ApprovalDecision.Pending)
            .OrderBy(a => a.Level)
            .FirstOrDefault();
        if (nextPending is null || nextPending.Id != approval.Id)
        {
            TempData["Error"] = "Approvals are decided sequentially — only the next pending step can be approved.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!string.Equals(Current.Role, approval.ApproverRole, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = $"Level {approval.Level} must be approved by a user in the {approval.ApproverRole} role.";
            return RedirectToAction(nameof(Details), new { id });
        }

        approval.Decision = ApprovalDecision.Approved;
        approval.ApproverId = Current.UserId;
        approval.DecidedAt = DateTime.UtcNow;
        approval.Comment = comment ?? string.Empty;

        if (permit.Approvals.All(a => a.Decision == ApprovalDecision.Approved))
        {
            permit.Status = PermitStatus.Approved;
            await Db.SaveChangesAsync();
            await _notifications.NotifyAsync(OrgId, permit.RequestedById,
                $"Permit {permit.Reference} ({permit.Title}) fully approved — ready to start.",
                Url.Action(nameof(Details), "Permits", new { id = permit.Id }) ?? string.Empty);
            await AuditAsync("Approve", "WorkPermit", permit.Id,
                $"Permit {permit.Reference} fully approved across all {permit.Approvals.Count} approval levels");
            TempData["Success"] = "Permit fully approved.";
        }
        else
        {
            await Db.SaveChangesAsync();
            var next = permit.Approvals
                .Where(a => a.Decision == ApprovalDecision.Pending)
                .OrderBy(a => a.Level)
                .FirstOrDefault();
            if (next is not null)
            {
                await _notifications.NotifyRoleAsync(OrgId, next.ApproverRole,
                    $"Permit {permit.Reference} ({permit.Title}) approved at Level {approval.Level} — your approval is now required at Level {next.Level}.",
                    Url.Action(nameof(Details), "Permits", new { id = permit.Id }) ?? string.Empty);
            }
            await AuditAsync("Approve", "WorkPermit", permit.Id,
                $"Level {approval.Level} ({approval.ApproverRole}) approved on permit {permit.Reference}");
            TempData["Success"] = $"Level {approval.Level} approved.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,SafetyOfficer,Admin")]
    public async Task<IActionResult> Reject(int id, int approvalId, string? comment)
    {
        var permit = await LoadPermitAsync(id);
        if (permit is null) return NotFound();

        if (permit.Status != PermitStatus.PendingApproval)
        {
            TempData["Error"] = "This permit is not awaiting approval.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var approval = permit.Approvals.FirstOrDefault(a => a.Id == approvalId);
        if (approval is null) return NotFound();

        var nextPending = permit.Approvals
            .Where(a => a.Decision == ApprovalDecision.Pending)
            .OrderBy(a => a.Level)
            .FirstOrDefault();
        if (nextPending is null || nextPending.Id != approval.Id)
        {
            TempData["Error"] = "Only the next pending approval step can be rejected.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!string.Equals(Current.Role, approval.ApproverRole, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = $"Level {approval.Level} must be rejected by a user in the {approval.ApproverRole} role.";
            return RedirectToAction(nameof(Details), new { id });
        }

        approval.Decision = ApprovalDecision.Rejected;
        approval.ApproverId = Current.UserId;
        approval.DecidedAt = DateTime.UtcNow;
        approval.Comment = comment ?? string.Empty;
        permit.Status = PermitStatus.Rejected;

        await Db.SaveChangesAsync();
await _notifications.NotifyAsync(OrgId, permit.RequestedById,
                $"Permit {permit.Reference} ({permit.Title}) rejected at Level {approval.Level} — please revise and resubmit.",
                Url.Action(nameof(Details), "Permits", new { id = permit.Id }) ?? string.Empty);
        await AuditAsync("Reject", "WorkPermit", permit.Id,
            $"Level {approval.Level} ({approval.ApproverRole}) rejected permit {permit.Reference}");
        TempData["Success"] = "Permit rejected.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id)
    {
        var permit = await LoadPermitAsync(id);
        if (permit is null) return NotFound();

        if (permit.Status != PermitStatus.Draft)
        {
            TempData["Error"] = "Only draft permits can be submitted for approval.";
            return RedirectToAction(nameof(Details), new { id });
        }

        permit.Status = PermitStatus.PendingApproval;
        await Db.SaveChangesAsync();

        var next = permit.Approvals
            .Where(a => a.Decision == ApprovalDecision.Pending)
            .OrderBy(a => a.Level)
            .FirstOrDefault();
        if (next is not null)
        {
            await _notifications.NotifyRoleAsync(OrgId, next.ApproverRole,
                $"Permit {permit.Reference} ({permit.Title}) submitted — {next.ApproverRole} approval required.",
                Url.Action(nameof(Details), "Permits", new { id = permit.Id }) ?? string.Empty);
        }

        await AuditAsync("Submit", "WorkPermit", permit.Id, $"Submitted permit {permit.Reference} for approval");
        TempData["Success"] = "Permit submitted for approval.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,SafetyOfficer,Admin")]
    public async Task<IActionResult> Start(int id)
    {
        var permit = await LoadPermitAsync(id);
        if (permit is null) return NotFound();

        if (permit.Status != PermitStatus.Approved)
        {
            TempData["Error"] = "Only approved permits can be started.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (permit.ChecklistItems.Any(c => !c.IsChecked))
        {
            TempData["Error"] = "All checklist items must be checked before the permit can be started.";
            return RedirectToAction(nameof(Details), new { id });
        }

        permit.Status = PermitStatus.Active;
        permit.ActualStart = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        await AuditAsync("Start", "WorkPermit", permit.Id, $"Started permit {permit.Reference}");
        TempData["Success"] = "Permit started and now active.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,SafetyOfficer,Admin")]
    public async Task<IActionResult> Suspend(int id)
    {
        var permit = await LoadPermitAsync(id);
        if (permit is null) return NotFound();

        if (permit.Status != PermitStatus.Active)
        {
            TempData["Error"] = "Only active permits can be suspended.";
            return RedirectToAction(nameof(Details), new { id });
        }

        permit.Status = PermitStatus.Suspended;
        await Db.SaveChangesAsync();
        await AuditAsync("Suspend", "WorkPermit", permit.Id, $"Suspended permit {permit.Reference}");
        TempData["Success"] = "Permit suspended.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,SafetyOfficer,Admin")]
    public async Task<IActionResult> Resume(int id)
    {
        var permit = await LoadPermitAsync(id);
        if (permit is null) return NotFound();

        if (permit.Status != PermitStatus.Suspended)
        {
            TempData["Error"] = "Only suspended permits can be resumed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        permit.Status = PermitStatus.Active;
        await Db.SaveChangesAsync();
        await AuditAsync("Resume", "WorkPermit", permit.Id, $"Resumed permit {permit.Reference}");
        TempData["Success"] = "Permit resumed and active again.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,SafetyOfficer,Admin")]
    public async Task<IActionResult> Close(int id)
    {
        var permit = await LoadPermitAsync(id);
        if (permit is null) return NotFound();

        if (permit.Status != PermitStatus.Active && permit.Status != PermitStatus.Suspended)
        {
            TempData["Error"] = "Only active or suspended permits can be closed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        permit.Status = PermitStatus.Closed;
        permit.ActualEnd = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        await AuditAsync("Close", "WorkPermit", permit.Id, $"Closed permit {permit.Reference}");
        TempData["Success"] = "Permit closed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task ValidateFormAsync(PermitFormViewModel model)
    {
        if (model.ProjectId <= 0 || !await Db.Projects
                .AnyAsync(p => p.Id == model.ProjectId && p.OrganizationId == OrgId))
            ModelState.AddModelError(nameof(model.ProjectId), "Select a valid project.");
        if (string.IsNullOrWhiteSpace(model.Title))
            ModelState.AddModelError(nameof(model.Title), "Title is required.");
        if (string.IsNullOrWhiteSpace(model.Location))
            ModelState.AddModelError(nameof(model.Location), "Location is required.");
        if (model.RequestedEnd <= model.RequestedStart)
            ModelState.AddModelError(nameof(model.RequestedEnd), "Requested end must be after requested start.");
    }

    private async Task<SelectList> ProjectOptionsAsync(int? selectedId = null)
    {
        var projects = await Db.Projects
            .Where(p => p.OrganizationId == OrgId)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync();
        return new SelectList(projects, "Id", "Name", selectedId);
    }

    private async Task<WorkPermit?> LoadPermitAsync(int id) =>
        await Db.WorkPermits
            .Include(w => w.Project)
            .Include(w => w.RequestedBy)
            .Include(w => w.ChecklistItems).ThenInclude(c => c.CheckedBy)
            .Include(w => w.Approvals).ThenInclude(a => a.Approver)
            .FirstOrDefaultAsync(w => w.Id == id && w.OrganizationId == OrgId);
}