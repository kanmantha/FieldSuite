using FieldSuite.Domain.Common;
using FieldSuite.Domain.Entities;
using FieldSuite.Infrastructure.Services;
using FieldSuite.Web.Services;
using FieldSuite.Web.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FieldSuite.Web.Controllers;

[Authorize]
public class QualityController : AppController
{
    private readonly INotificationService _notifications;

    public QualityController(AppDbContext db, ICurrentUser current, IAuditService audit, INotificationService notifications)
        : base(db, current, audit)
    {
        _notifications = notifications;
    }

    private async Task<List<Project>> OrgProjectsAsync() =>
        await Db.Projects.Where(p => p.OrganizationId == OrgId && p.IsActive).OrderBy(p => p.Name).ToListAsync();

    private async Task<List<AppUser>> OrgUsersAsync() =>
        await Db.Users.Where(u => u.OrganizationId == OrgId).OrderBy(u => u.FullName).ToListAsync();

    private static string TempRef() => "TMP-" + Guid.NewGuid().ToString("N")[..24];

    // ---------- Snags ----------
    public async Task<IActionResult> Index(SnagStatus? status, int? projectId)
    {
        IQueryable<Snag> query = Db.Snags.AsNoTracking()
            .Where(s => s.Project!.OrganizationId == OrgId)
            .Include(s => s.Project).Include(s => s.RaisedBy).Include(s => s.AssignedTo);

        if (status.HasValue) query = query.Where(s => s.Status == status.Value);
        if (projectId.HasValue) query = query.Where(s => s.ProjectId == projectId.Value);

        var list = await query.OrderByDescending(s => s.DueDate == null).ThenByDescending(s => s.Id).ToListAsync();

        ViewBag.OpenCount = await Db.Snags.CountAsync(s => s.Project!.OrganizationId == OrgId
            && s.Status != SnagStatus.Closed && s.Status != SnagStatus.Rejected);
        ViewBag.OverdueCount = await Db.Snags.CountAsync(s => s.Project!.OrganizationId == OrgId
            && s.Status != SnagStatus.Closed && s.Status != SnagStatus.Rejected
            && s.DueDate != null && s.DueDate < DateTime.UtcNow.Date);
        ViewBag.Status = status;
        ViewBag.ProjectId = projectId;
        ViewBag.Projects = new SelectList(await Db.Projects.Where(p => p.OrganizationId == OrgId).OrderBy(p => p.Name).ToListAsync(), "Id", "Name", projectId);
        ViewData["Title"] = "Snag / Punch List";
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> SnagCreate()
    {
        ViewData["Title"] = "Raise Snag";
        ViewBag.Projects = new SelectList(await OrgProjectsAsync(), "Id", "Name");
        ViewBag.Users = new SelectList(await OrgUsersAsync(), "Id", "FullName");
        return View(new SnagCreateVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SnagCreate(SnagCreateVm vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Projects = new SelectList(await OrgProjectsAsync(), "Id", "Name", vm.ProjectId);
            ViewBag.Users = new SelectList(await OrgUsersAsync(), "Id", "FullName", vm.AssignedToId);
            ViewData["Title"] = "Raise Snag";
            return View(vm);
        }

        string? photoPath = null;
        if (vm.Photo is { Length: > 0 })
        {
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var ext = Path.GetExtension(vm.Photo.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext) || vm.Photo.Length > 5 * 1024 * 1024)
            {
                TempData["Error"] = "Photo must be jpg/png/gif/webp and under 5 MB.";
                ViewBag.Projects = new SelectList(await OrgProjectsAsync(), "Id", "Name", vm.ProjectId);
                ViewBag.Users = new SelectList(await OrgUsersAsync(), "Id", "FullName", vm.AssignedToId);
                return View(vm);
            }
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            Directory.CreateDirectory(dir);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            using var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create);
            await vm.Photo.CopyToAsync(stream);
            photoPath = $"uploads/{fileName}";
        }

        var snag = new Snag
        {
            ProjectId = vm.ProjectId,
            Reference = TempRef(),
            Title = vm.Title.Trim(),
            Description = vm.Description.Trim(),
            Location = vm.Location.Trim(),
            Priority = vm.Priority,
            RaisedById = Current.UserId,
            AssignedToId = string.IsNullOrWhiteSpace(vm.AssignedToId) ? null : vm.AssignedToId,
            DueDate = vm.DueDate,
            PhotoPath = photoPath,
            Status = string.IsNullOrWhiteSpace(vm.AssignedToId) ? SnagStatus.Open : SnagStatus.Assigned
        };
        Db.Snags.Add(snag);
        await Db.SaveChangesAsync();
        snag.Reference = $"SNG-{snag.Id:D4}";
        await Db.SaveChangesAsync();

        await AuditAsync("Create", "Snag", snag.Id, $"Priority={snag.Priority}, Title={snag.Title}");
        if (!string.IsNullOrWhiteSpace(snag.AssignedToId))
            await _notifications.NotifyAsync(OrgId, snag.AssignedToId, $"Snag {snag.Reference} assigned: {snag.Title}", $"/Quality/SnagDetails/{snag.Id}");

        TempData["Success"] = $"Snag {snag.Reference} raised.";
        return RedirectToAction(nameof(SnagDetails), new { id = snag.Id });
    }

    public async Task<IActionResult> SnagDetails(int id)
    {
        var snag = await Db.Snags.AsNoTracking()
            .Where(s => s.Id == id && s.Project!.OrganizationId == OrgId)
            .Include(s => s.Project).Include(s => s.RaisedBy).Include(s => s.AssignedTo)
            .FirstOrDefaultAsync();
        if (snag is null) return NotFound();
        ViewData["Title"] = $"Snag {snag.Reference}";
        return View(snag);
    }

    [HttpGet]
    public async Task<IActionResult> SnagEdit(int id)
    {
        var snag = await Db.Snags.FirstOrDefaultAsync(s => s.Id == id && s.Project!.OrganizationId == OrgId);
        if (snag is null) return NotFound();
        if (snag.Status is SnagStatus.Closed or SnagStatus.Rejected)
        {
            TempData["Error"] = "Closed/rejected snags cannot be edited.";
            return RedirectToAction(nameof(SnagDetails), new { id });
        }
        ViewData["Title"] = "Edit Snag";
        ViewBag.Projects = new SelectList(await OrgProjectsAsync(), "Id", "Name", snag.ProjectId);
        ViewBag.Users = new SelectList(await OrgUsersAsync(), "Id", "FullName", snag.AssignedToId);
        return View(new SnagEditVm
        {
            Id = snag.Id, ProjectId = snag.ProjectId, Title = snag.Title, Description = snag.Description,
            Location = snag.Location, Priority = snag.Priority, AssignedToId = snag.AssignedToId, DueDate = snag.DueDate
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SnagEdit(SnagEditVm vm)
    {
        var snag = await Db.Snags.FirstOrDefaultAsync(s => s.Id == vm.Id && s.Project!.OrganizationId == OrgId);
        if (snag is null) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewBag.Projects = new SelectList(await OrgProjectsAsync(), "Id", "Name", vm.ProjectId);
            ViewBag.Users = new SelectList(await OrgUsersAsync(), "Id", "FullName", vm.AssignedToId);
            ViewData["Title"] = "Edit Snag";
            return View(vm);
        }
        snag.ProjectId = vm.ProjectId;
        snag.Title = vm.Title.Trim();
        snag.Description = vm.Description.Trim();
        snag.Location = vm.Location.Trim();
        snag.Priority = vm.Priority;
        snag.AssignedToId = string.IsNullOrWhiteSpace(vm.AssignedToId) ? null : vm.AssignedToId;
        snag.DueDate = vm.DueDate;
        await Db.SaveChangesAsync();
        await AuditAsync("Update", "Snag", snag.Id, "Edited snag");
        TempData["Success"] = "Snag updated.";
        return RedirectToAction(nameof(SnagDetails), new { id = snag.Id });
    }

    [HttpPost]
    [Authorize(Roles = "QCInspector,SiteManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SnagTransition(int id, SnagStatus to)
    {
        var snag = await Db.Snags.FirstOrDefaultAsync(s => s.Id == id && s.Project!.OrganizationId == OrgId);
        if (snag is null) return NotFound();

        var valid = snag.Status switch
        {
            SnagStatus.Open => to is SnagStatus.Assigned or SnagStatus.InProgress,
            SnagStatus.Assigned => to == SnagStatus.InProgress,
            SnagStatus.InProgress => to == SnagStatus.Retest,
            SnagStatus.Retest => to is SnagStatus.Closed or SnagStatus.Rejected,
            SnagStatus.Rejected => to == SnagStatus.Assigned,
            _ => false
        };
        if (!valid)
        {
            TempData["Error"] = $"Cannot move from {snag.Status} to {to}.";
            return RedirectToAction(nameof(SnagDetails), new { id });
        }

        snag.Status = to;
        if (to == SnagStatus.Closed) snag.ClosedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        await AuditAsync("Transition", "Snag", snag.Id, $"Status -> {to}");
        TempData["Success"] = $"Snag moved to {to}.";
        return RedirectToAction(nameof(SnagDetails), new { id });
    }

    // ---------- Inspections ----------
    public async Task<IActionResult> Inspections()
    {
        var list = await Db.Inspections.AsNoTracking()
            .Where(i => i.Project!.OrganizationId == OrgId)
            .Include(i => i.Project).Include(i => i.Inspector).Include(i => i.Items)
            .OrderByDescending(i => i.InspectionDate)
            .ToListAsync();
        ViewData["Title"] = "Inspections";
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> InspectionCreate()
    {
        ViewData["Title"] = "New Inspection";
        ViewBag.Projects = new SelectList(await OrgProjectsAsync(), "Id", "Name");
        return View(new InspectionCreateVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InspectionCreate(InspectionCreateVm vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Projects = new SelectList(await OrgProjectsAsync(), "Id", "Name", vm.ProjectId);
            ViewData["Title"] = "New Inspection";
            return View(vm);
        }
        var inspection = new Inspection
        {
            ProjectId = vm.ProjectId,
            Reference = TempRef(),
            Type = vm.Type,
            Title = vm.Title.Trim(),
            InspectionDate = vm.InspectionDate,
            InspectorId = Current.UserId,
            Status = InspectionStatus.Draft
        };
        Db.Inspections.Add(inspection);
        await Db.SaveChangesAsync();
        inspection.Reference = $"QIN-{inspection.Id:D4}";
        await Db.SaveChangesAsync();
        await AuditAsync("Create", "Inspection", inspection.Id, inspection.Title);
        TempData["Success"] = $"Inspection {inspection.Reference} created — add checklist items.";
        return RedirectToAction(nameof(InspectionDetails), new { id = inspection.Id });
    }

    public async Task<IActionResult> InspectionDetails(int id)
    {
        var inspection = await Db.Inspections.AsNoTracking()
            .Where(i => i.Id == id && i.Project!.OrganizationId == OrgId)
            .Include(i => i.Project).Include(i => i.Inspector).Include(i => i.Items)
            .FirstOrDefaultAsync();
        if (inspection is null) return NotFound();
        ViewData["Title"] = $"Inspection {inspection.Reference}";
        return View(inspection);
    }

    [HttpPost]
    [Authorize(Roles = "QCInspector,SiteManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InspectionItemAdd(int inspectionId, string checklistText)
    {
        var inspection = await Db.Inspections
            .FirstOrDefaultAsync(i => i.Id == inspectionId && i.Project!.OrganizationId == OrgId);
        if (inspection is null) return NotFound();
        if (string.IsNullOrWhiteSpace(checklistText))
        {
            TempData["Error"] = "Checklist text is required.";
            return RedirectToAction(nameof(InspectionDetails), new { id = inspectionId });
        }
        Db.InspectionItems.Add(new InspectionItem { InspectionId = inspectionId, ChecklistText = checklistText.Trim() });
        if (inspection.Status == InspectionStatus.Draft) inspection.Status = InspectionStatus.InProgress;
        await Db.SaveChangesAsync();
        await AuditAsync("Update", "Inspection", inspection.Id, "Item added");
        return RedirectToAction(nameof(InspectionDetails), new { id = inspectionId });
    }

    [HttpPost]
    [Authorize(Roles = "QCInspector,SiteManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InspectionItemResult(int itemId, CheckResult result, string? comment)
    {
        var item = await Db.InspectionItems.Include(i => i.Inspection).ThenInclude(i => i!.Project)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.Inspection!.Project!.OrganizationId == OrgId);
        if (item is null) return NotFound();
        item.Result = result;
        item.Comment = comment ?? string.Empty;
        await Db.SaveChangesAsync();
        return RedirectToAction(nameof(InspectionDetails), new { id = item.InspectionId });
    }

    [HttpPost]
    [Authorize(Roles = "QCInspector,SiteManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InspectionFinalize(int id)
    {
        var inspection = await Db.Inspections
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.Project!.OrganizationId == OrgId);
        if (inspection is null) return NotFound();
        if (inspection.Items.Count == 0)
        {
            TempData["Error"] = "Add at least one checklist item before finalizing.";
            return RedirectToAction(nameof(InspectionDetails), new { id });
        }
        inspection.Status = inspection.Items.Any(i => i.Result == CheckResult.Fail)
            ? InspectionStatus.Failed : InspectionStatus.Passed;
        await Db.SaveChangesAsync();
        await AuditAsync("Finalize", "Inspection", inspection.Id, $"Status -> {inspection.Status}");
        TempData["Success"] = $"Inspection finalized: {inspection.Status}.";
        if (inspection.Status == InspectionStatus.Failed)
            await _notifications.NotifyRoleAsync(OrgId, "SiteManager",
                $"Inspection {inspection.Reference} FAILED — raise snags", $"/Quality/InspectionDetails/{inspection.Id}");
        return RedirectToAction(nameof(InspectionDetails), new { id });
    }

    // ---------- NCRs ----------
    public async Task<IActionResult> NCRs()
    {
        var list = await Db.NCRs.AsNoTracking()
            .Where(n => n.Project!.OrganizationId == OrgId)
            .Include(n => n.Project)
            .OrderByDescending(n => n.DetectedDate)
            .ToListAsync();
        ViewData["Title"] = "Non-Conformance Reports";
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> NCRCreate()
    {
        ViewData["Title"] = "New NCR";
        ViewBag.Projects = new SelectList(await OrgProjectsAsync(), "Id", "Name");
        return View(new NcrCreateVm { DetectedDate = DateTime.UtcNow.Date });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NCRCreate(NcrCreateVm vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Projects = new SelectList(await OrgProjectsAsync(), "Id", "Name", vm.ProjectId);
            ViewData["Title"] = "New NCR";
            return View(vm);
        }
        var ncr = new NCR
        {
            ProjectId = vm.ProjectId,
            Reference = TempRef(),
            Title = vm.Title.Trim(),
            Severity = vm.Severity,
            Description = vm.Description.Trim(),
            DetectedDate = vm.DetectedDate,
            ContainmentAction = vm.ContainmentAction.Trim(),
            Status = NCRStatus.Open
        };
        Db.NCRs.Add(ncr);
        await Db.SaveChangesAsync();
        ncr.Reference = $"NCR-{ncr.Id:D4}";
        await Db.SaveChangesAsync();
        await AuditAsync("Create", "NCR", ncr.Id, ncr.Title);
        await _notifications.NotifyRoleAsync(OrgId, "QCInspector", $"New NCR {ncr.Reference}: {ncr.Title}", $"/Quality/NCRDetails/{ncr.Id}");
        TempData["Success"] = $"NCR {ncr.Reference} raised.";
        return RedirectToAction(nameof(NCRDetails), new { id = ncr.Id });
    }

    public async Task<IActionResult> NCRDetails(int id)
    {
        var ncr = await Db.NCRs.AsNoTracking()
            .Where(n => n.Id == id && n.Project!.OrganizationId == OrgId)
            .Include(n => n.Project)
            .FirstOrDefaultAsync();
        if (ncr is null) return NotFound();
        ViewData["Title"] = $"NCR {ncr.Reference}";
        return View(ncr);
    }

    [HttpPost]
    [Authorize(Roles = "QCInspector,SiteManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NCRTransition(int id, NCRStatus to)
    {
        var ncr = await Db.NCRs.FirstOrDefaultAsync(n => n.Id == id && n.Project!.OrganizationId == OrgId);
        if (ncr is null) return NotFound();
        var valid = ncr.Status switch
        {
            NCRStatus.Open => to == NCRStatus.Containment,
            NCRStatus.Containment => to == NCRStatus.Investigation,
            NCRStatus.Investigation => to == NCRStatus.Closed,
            _ => false
        };
        if (!valid)
        {
            TempData["Error"] = $"Cannot move from {ncr.Status} to {to}.";
            return RedirectToAction(nameof(NCRDetails), new { id });
        }
        ncr.Status = to;
        await Db.SaveChangesAsync();
        await AuditAsync("Transition", "NCR", ncr.Id, $"Status -> {to}");
        TempData["Success"] = $"NCR moved to {to}.";
        return RedirectToAction(nameof(NCRDetails), new { id });
    }
}
