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
public class WorkforceController : AppController
{
    public WorkforceController(AppDbContext db, ICurrentUser current, IAuditService audit)
        : base(db, current, audit) { }

    // ==================== TODAY'S ATTENDANCE BOARD ====================

    /// <summary>
    /// Lists records.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var today = DateTime.UtcNow.Date;

        var workers = await Db.Workers
            .Where(w => w.OrganizationId == OrgId && w.IsActive)
            .OrderBy(w => w.Shift)
            .ThenBy(w => w.FullName)
            .ToListAsync();

        var workerIds = workers.Select(w => w.Id).ToList();

        var todayRows = await Db.Attendances
            .Where(a => a.Worker!.OrganizationId == OrgId
                        && a.WorkDate == today
                        && workerIds.Contains(a.WorkerId))
            .Include(a => a.Project)
            .ToListAsync();

        var byWorker = todayRows.ToDictionary(a => a.WorkerId, a => a);
        var projects = await ProjectOptionsAsync(activeOnly: true);

        var model = new AttendanceBoardViewModel
        {
            Date = today,
            Projects = projects,
            PresentCount = todayRows.Count(a => a.Status == AttendanceStatus.Present),
            AbsentCount = todayRows.Count(a => a.Status == AttendanceStatus.Absent),
            LateCount = todayRows.Count(a => a.Status == AttendanceStatus.Late),
            LeaveCount = todayRows.Count(a => a.Status == AttendanceStatus.OnLeave)
        };

        foreach (var worker in workers)
        {
            byWorker.TryGetValue(worker.Id, out var todayRow);
            model.Rows.Add(new AttendanceBoardRow
            {
                Worker = worker,
                Today = todayRow,
                StatusOptions = StatusOptions(todayRow?.Status),
                ProjectOptions = CloneSelected(projects, todayRow?.ProjectId)
            });
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,SafetyOfficer,Admin")]
    public async Task<IActionResult> AttendanceMark(int workerId, AttendanceStatus status, int projectId)
    {
        if (workerId <= 0)
        {
            TempData["Error"] = "Select a worker.";
            return RedirectToAction(nameof(Index));
        }

        var worker = await Db.Workers
            .FirstOrDefaultAsync(w => w.Id == workerId && w.OrganizationId == OrgId);
        if (worker is null) return NotFound();

        if (projectId <= 0)
        {
            TempData["Error"] = "Select a project before marking attendance.";
            return RedirectToAction(nameof(Index));
        }

        if (!await Db.Projects.AnyAsync(p => p.Id == projectId && p.OrganizationId == OrgId))
            return NotFound();

        var today = DateTime.UtcNow.Date;
        var now = DateTime.UtcNow;

        var row = await Db.Attendances
            .FirstOrDefaultAsync(a => a.WorkerId == workerId && a.WorkDate == today);

        var created = row is null;
        if (row is null)
        {
            row = new Attendance { WorkerId = workerId, WorkDate = today };
            Db.Attendances.Add(row);
        }

        row.ProjectId = projectId;
        row.Status = status;

        if (status == AttendanceStatus.Present || status == AttendanceStatus.Late)
        {
            row.ClockIn ??= now;
        }
        else
        {
            row.ClockIn = null;
            row.ClockOut = null;
            row.HoursWorked = null;
        }

        await Db.SaveChangesAsync();
        await AuditAsync(created ? "Create" : "Update", "Attendance", row.Id,
            $"{worker.FullName} marked {status} for {today:dd MMM yyyy}");
        TempData["Success"] = $"{worker.FullName} marked {status}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,SafetyOfficer,Admin")]
    public async Task<IActionResult> AttendanceClockOut(int workerId)
    {
        var worker = await Db.Workers
            .FirstOrDefaultAsync(w => w.Id == workerId && w.OrganizationId == OrgId);
        if (worker is null) return NotFound();

        var today = DateTime.UtcNow.Date;
        var row = await Db.Attendances
            .FirstOrDefaultAsync(a => a.WorkerId == workerId && a.WorkDate == today);

        if (row is null || row.ClockIn is null)
        {
            TempData["Error"] = $"{worker.FullName} has no clock-in recorded for today.";
            return RedirectToAction(nameof(Index));
        }

        var now = DateTime.UtcNow;
        row.ClockOut = now;
        row.HoursWorked = (decimal)Math.Round((now - row.ClockIn.Value).TotalHours, 2);

        await Db.SaveChangesAsync();
        await AuditAsync("Update", "Attendance", row.Id,
            $"{worker.FullName} clocked out at {now:HH:mm} ({row.HoursWorked:N2} hrs)");
        TempData["Success"] = $"{worker.FullName} clocked out — {row.HoursWorked:N2} hours.";
        return RedirectToAction(nameof(Index));
    }

    // ==================== ATTENDANCE HISTORY ====================

    public async Task<IActionResult> Attendance(DateTime? date, int? projectId)
    {
        IQueryable<Attendance> query = Db.Attendances
            .Where(a => a.Worker!.OrganizationId == OrgId);

        if (date.HasValue)
        {
            var day = date.Value.Date;
            query = query.Where(a => a.WorkDate == day);
        }

        if (projectId.HasValue)
            query = query.Where(a => a.ProjectId == projectId.Value);

        var rows = await query
            .Include(a => a.Worker)
            .Include(a => a.Project)
            .OrderByDescending(a => a.WorkDate)
            .ThenBy(a => a.Worker!.FullName)
            .Take(300)
            .ToListAsync();

        var model = new AttendanceHistoryViewModel
        {
            Date = date?.Date,
            ProjectId = projectId,
            Rows = rows,
            Projects = await ProjectOptionsAsync(selectedId: projectId),
            Workers = await WorkerOptionsAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,SafetyOfficer,Admin")]
    public async Task<IActionResult> AttendanceCreate(int workerId, int projectId, DateTime? workDate,
        AttendanceStatus status, string? clockIn, string? clockOut)
    {
        if (workerId <= 0)
        {
            TempData["Error"] = "Select a worker.";
            return RedirectToAction("Attendance");
        }

        var worker = await Db.Workers
            .FirstOrDefaultAsync(w => w.Id == workerId && w.OrganizationId == OrgId);
        if (worker is null) return NotFound();

        if (projectId <= 0)
        {
            TempData["Error"] = "Select a project before recording attendance.";
            return RedirectToAction("Attendance");
        }

        if (!await Db.Projects.AnyAsync(p => p.Id == projectId && p.OrganizationId == OrgId))
            return NotFound();

        var day = (workDate ?? DateTime.UtcNow.Date).Date;

        DateTime? clockInAt = null;
        if (!string.IsNullOrWhiteSpace(clockIn))
        {
            if (!TimeSpan.TryParse(clockIn.Trim(), out var inTime))
            {
                TempData["Error"] = "Clock-in time is not a valid time.";
                return RedirectToAction("Attendance");
            }
            clockInAt = day.Add(inTime);
        }

        DateTime? clockOutAt = null;
        if (!string.IsNullOrWhiteSpace(clockOut))
        {
            if (!TimeSpan.TryParse(clockOut.Trim(), out var outTime))
            {
                TempData["Error"] = "Clock-out time is not a valid time.";
                return RedirectToAction("Attendance");
            }
            clockOutAt = day.Add(outTime);
        }

        if (clockInAt.HasValue && clockOutAt.HasValue && clockOutAt.Value <= clockInAt.Value)
        {
            TempData["Error"] = "Clock-out must be after clock-in.";
            return RedirectToAction("Attendance");
        }

        var row = await Db.Attendances
            .FirstOrDefaultAsync(a => a.WorkerId == workerId && a.WorkDate == day);

        var created = row is null;
        if (row is null)
        {
            row = new Attendance { WorkerId = workerId, WorkDate = day };
            Db.Attendances.Add(row);
        }

        row.ProjectId = projectId;
        row.Status = status;
        row.ClockIn = clockInAt;
        row.ClockOut = clockOutAt;
        row.HoursWorked = clockInAt.HasValue && clockOutAt.HasValue
            ? (decimal)Math.Round((clockOutAt.Value - clockInAt.Value).TotalHours, 2)
            : null;

        await Db.SaveChangesAsync();
        await AuditAsync(created ? "Create" : "Update", "Attendance", row.Id,
            $"{worker.FullName} — {status} on {day:dd MMM yyyy}");
        TempData["Success"] = created
            ? $"Attendance recorded for {worker.FullName}."
            : $"Attendance updated for {worker.FullName}.";
        return RedirectToAction("Attendance", new { date = day.ToString("yyyy-MM-dd"), projectId });
    }

    // ==================== WORKERS ====================

    public async Task<IActionResult> Workers(bool? active = true)
    {
        var query = Db.Workers.Where(w => w.OrganizationId == OrgId);
        if (active.HasValue) query = query.Where(w => w.IsActive == active.Value);

        var workers = await query
            .Include(w => w.ContractorCompany)
            .OrderBy(w => w.FullName)
            .ToListAsync();

        var workerIds = workers.Select(w => w.Id).ToList();
        var today = DateTime.UtcNow.Date;
        var horizon = today.AddDays(30);

        var expiringDocs = await Db.OnboardingDocuments
            .Where(d => d.Worker!.OrganizationId == OrgId
                        && workerIds.Contains(d.WorkerId)
                        && d.ExpiryDate != null)
            .Select(d => new { d.WorkerId, ExpiryDate = d.ExpiryDate!.Value })
            .ToListAsync();

        var model = new WorkersViewModel
        {
            Active = active,
            Rows = workers.Select(w => new WorkerListRow
            {
                Worker = w,
                ExpiredDocs = expiringDocs.Count(d => d.WorkerId == w.Id && d.ExpiryDate < today),
                ExpiringDocs = expiringDocs.Count(d => d.WorkerId == w.Id && d.ExpiryDate >= today && d.ExpiryDate <= horizon)
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "SiteManager,Admin")]
    public async Task<IActionResult> WorkerCreate()
    {
        return View(new WorkerFormViewModel
        {
            HireDate = DateTime.UtcNow.Date,
            Contractors = await ContractorOptionsAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,Admin")]
    public async Task<IActionResult> WorkerCreate(WorkerFormViewModel model)
    {
        var fullName = model.FullName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fullName))
            ModelState.AddModelError(nameof(model.FullName), "Full name is required.");

        if (model.ContractorCompanyId.HasValue &&
            !await Db.ContractorCompanies.AnyAsync(c => c.Id == model.ContractorCompanyId && c.OrganizationId == OrgId))
            ModelState.AddModelError(nameof(model.ContractorCompanyId), "Select a valid contractor.");

        if (!ModelState.IsValid)
        {
            model.Contractors = await ContractorOptionsAsync(model.ContractorCompanyId);
            return View(model);
        }

        var worker = new Worker
        {
            OrganizationId = OrgId,
            FullName = fullName,
            JobTitle = model.JobTitle?.Trim() ?? string.Empty,
            Phone = model.Phone?.Trim() ?? string.Empty,
            Email = model.Email?.Trim() ?? string.Empty,
            HireDate = model.HireDate.Date,
            Shift = model.Shift,
            IsActive = model.IsActive,
            ContractorCompanyId = model.ContractorCompanyId
        };

        Db.Workers.Add(worker);
        await Db.SaveChangesAsync();
        await AuditAsync("Create", "Worker", worker.Id, $"Created worker {worker.FullName}");
        TempData["Success"] = "Worker created.";
        return RedirectToAction(nameof(WorkerDetails), new { id = worker.Id });
    }

    public async Task<IActionResult> WorkerDetails(int id)
    {
        var worker = await Db.Workers
            .Include(w => w.ContractorCompany)
            .FirstOrDefaultAsync(w => w.Id == id && w.OrganizationId == OrgId);
        if (worker is null) return NotFound();

        var documents = await Db.OnboardingDocuments
            .Where(d => d.Worker!.OrganizationId == OrgId && d.WorkerId == id)
            .OrderByDescending(d => d.ExpiryDate.HasValue)
            .ThenBy(d => d.ExpiryDate)
            .ToListAsync();

        var recent = await Db.Attendances
            .Where(a => a.Worker!.OrganizationId == OrgId && a.WorkerId == id)
            .Include(a => a.Project)
            .OrderByDescending(a => a.WorkDate)
            .ThenByDescending(a => a.Id)
            .Take(15)
            .ToListAsync();

        return View(new WorkerDetailsViewModel
        {
            Worker = worker,
            Documents = documents,
            RecentAttendance = recent
        });
    }

    [HttpGet]
    [Authorize(Roles = "SiteManager,Admin")]
    public async Task<IActionResult> WorkerEdit(int id)
    {
        var worker = await Db.Workers
            .FirstOrDefaultAsync(w => w.Id == id && w.OrganizationId == OrgId);
        if (worker is null) return NotFound();

        return View(new WorkerFormViewModel
        {
            Id = worker.Id,
            FullName = worker.FullName,
            JobTitle = worker.JobTitle,
            Phone = worker.Phone,
            Email = worker.Email,
            HireDate = worker.HireDate,
            Shift = worker.Shift,
            IsActive = worker.IsActive,
            ContractorCompanyId = worker.ContractorCompanyId,
            Contractors = await ContractorOptionsAsync(worker.ContractorCompanyId)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,Admin")]
    public async Task<IActionResult> WorkerEdit(int id, WorkerFormViewModel model)
    {
        var worker = await Db.Workers
            .FirstOrDefaultAsync(w => w.Id == id && w.OrganizationId == OrgId);
        if (worker is null) return NotFound();

        var fullName = model.FullName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fullName))
            ModelState.AddModelError(nameof(model.FullName), "Full name is required.");

        if (model.ContractorCompanyId.HasValue &&
            !await Db.ContractorCompanies.AnyAsync(c => c.Id == model.ContractorCompanyId && c.OrganizationId == OrgId))
            ModelState.AddModelError(nameof(model.ContractorCompanyId), "Select a valid contractor.");

        if (!ModelState.IsValid)
        {
            model.Id = worker.Id;
            model.Contractors = await ContractorOptionsAsync(model.ContractorCompanyId);
            return View(model);
        }

        worker.FullName = fullName;
        worker.JobTitle = model.JobTitle?.Trim() ?? string.Empty;
        worker.Phone = model.Phone?.Trim() ?? string.Empty;
        worker.Email = model.Email?.Trim() ?? string.Empty;
        worker.HireDate = model.HireDate.Date;
        worker.Shift = model.Shift;
        worker.IsActive = model.IsActive;
        worker.ContractorCompanyId = model.ContractorCompanyId;

        await Db.SaveChangesAsync();
        await AuditAsync("Update", "Worker", worker.Id, $"Updated worker {worker.FullName}");
        TempData["Success"] = "Worker updated.";
        return RedirectToAction(nameof(WorkerDetails), new { id = worker.Id });
    }

    // ==================== ONBOARDING DOCUMENTS ====================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,SafetyOfficer,Admin")]
    public async Task<IActionResult> DocumentAdd(int workerId, WorkerDocumentType type, string documentNumber,
        DateTime? issuedDate, DateTime? expiryDate, string? filePath)
    {
        var worker = await Db.Workers
            .FirstOrDefaultAsync(w => w.Id == workerId && w.OrganizationId == OrgId);
        if (worker is null) return NotFound();

        if (issuedDate.HasValue && expiryDate.HasValue && expiryDate.Value.Date < issuedDate.Value.Date)
        {
            TempData["Error"] = "Expiry date cannot be before the issue date.";
            return RedirectToAction(nameof(WorkerDetails), new { id = workerId });
        }

        var document = new OnboardingDocument
        {
            WorkerId = workerId,
            Type = type,
            DocumentNumber = documentNumber?.Trim() ?? string.Empty,
            IssuedDate = issuedDate?.Date,
            ExpiryDate = expiryDate?.Date,
            FilePath = filePath?.Trim() ?? string.Empty
        };

        Db.OnboardingDocuments.Add(document);
        await Db.SaveChangesAsync();
        await AuditAsync("Create", "OnboardingDocument", document.Id,
            $"Added {type} document for {worker.FullName}");
        TempData["Success"] = "Document added.";
        return RedirectToAction(nameof(WorkerDetails), new { id = workerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,SafetyOfficer,Admin")]
    public async Task<IActionResult> DocumentDelete(int id)
    {
        var document = await Db.OnboardingDocuments
            .Include(d => d.Worker)
            .FirstOrDefaultAsync(d => d.Id == id && d.Worker!.OrganizationId == OrgId);
        if (document is null) return NotFound();

        var workerId = document.WorkerId;
        var label = $"{document.Type} {document.DocumentNumber}".Trim();

        Db.OnboardingDocuments.Remove(document);
        await Db.SaveChangesAsync();
        await AuditAsync("Delete", "OnboardingDocument", document.Id, $"Deleted {label}");
        TempData["Success"] = "Document deleted.";
        return RedirectToAction(nameof(WorkerDetails), new { id = workerId });
    }

    // ==================== CONTRACTORS ====================

    public async Task<IActionResult> Contractors(bool? active)
    {
        var query = Db.ContractorCompanies.Where(c => c.OrganizationId == OrgId);
        if (active.HasValue) query = query.Where(c => c.IsActive == active.Value);

        var contractors = await query
            .Include(c => c.Workers)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var model = new ContractorsViewModel
        {
            Active = active,
            Rows = contractors.Select(c => new ContractorListRow
            {
                Contractor = c,
                WorkerCount = c.Workers.Count
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "SiteManager,Admin")]
    public IActionResult ContractorCreate() => View(new ContractorFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,Admin")]
    public async Task<IActionResult> ContractorCreate(ContractorFormViewModel model)
    {
        var name = model.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            ModelState.AddModelError(nameof(model.Name), "Company name is required.");

        if (!ModelState.IsValid) return View(model);

        var contractor = new ContractorCompany
        {
            OrganizationId = OrgId,
            Name = name,
            ContactPerson = model.ContactPerson?.Trim() ?? string.Empty,
            Phone = model.Phone?.Trim() ?? string.Empty,
            Email = model.Email?.Trim() ?? string.Empty,
            Trade = model.Trade?.Trim() ?? string.Empty,
            IsActive = model.IsActive
        };

        Db.ContractorCompanies.Add(contractor);
        await Db.SaveChangesAsync();
        await AuditAsync("Create", "ContractorCompany", contractor.Id, $"Created contractor {contractor.Name}");
        TempData["Success"] = "Contractor created.";
        return RedirectToAction(nameof(Contractors));
    }

    [HttpGet]
    [Authorize(Roles = "SiteManager,Admin")]
    public async Task<IActionResult> ContractorEdit(int id)
    {
        var contractor = await Db.ContractorCompanies
            .FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == OrgId);
        if (contractor is null) return NotFound();

        return View(new ContractorFormViewModel
        {
            Id = contractor.Id,
            Name = contractor.Name,
            ContactPerson = contractor.ContactPerson,
            Phone = contractor.Phone,
            Email = contractor.Email,
            Trade = contractor.Trade,
            IsActive = contractor.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,Admin")]
    public async Task<IActionResult> ContractorEdit(int id, ContractorFormViewModel model)
    {
        var contractor = await Db.ContractorCompanies
            .FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == OrgId);
        if (contractor is null) return NotFound();

        var name = model.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            ModelState.AddModelError(nameof(model.Name), "Company name is required.");

        if (!ModelState.IsValid)
        {
            model.Id = contractor.Id;
            return View(model);
        }

        contractor.Name = name;
        contractor.ContactPerson = model.ContactPerson?.Trim() ?? string.Empty;
        contractor.Phone = model.Phone?.Trim() ?? string.Empty;
        contractor.Email = model.Email?.Trim() ?? string.Empty;
        contractor.Trade = model.Trade?.Trim() ?? string.Empty;
        contractor.IsActive = model.IsActive;

        await Db.SaveChangesAsync();
        await AuditAsync("Update", "ContractorCompany", contractor.Id, $"Updated contractor {contractor.Name}");
        TempData["Success"] = "Contractor updated.";
        return RedirectToAction(nameof(Contractors));
    }

    // ==================== HELPERS ====================

    private async Task<List<SelectListItem>> ProjectOptionsAsync(bool activeOnly = false, int? selectedId = null)
    {
        var query = Db.Projects.Where(p => p.OrganizationId == OrgId);
        if (activeOnly) query = query.Where(p => p.IsActive);

        var projects = await query
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Code, p.Name })
            .ToListAsync();

        return projects
            .Select(p => new SelectListItem($"{p.Code} - {p.Name}", p.Id.ToString(),
                selectedId.HasValue && p.Id == selectedId.Value))
            .ToList();
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

    private async Task<List<ContractorCompany>> ContractorOptionsAsync(int? selectedId = null) =>
        await Db.ContractorCompanies
            .Where(c => c.OrganizationId == OrgId
                        && (c.IsActive || c.Id == (selectedId ?? -1)))
            .OrderBy(c => c.Name)
            .ToListAsync();

    private static List<SelectListItem> StatusOptions(AttendanceStatus? current) =>
        Enum.GetValues<AttendanceStatus>()
            .Select(s => new SelectListItem(s.ToString(), ((int)s).ToString(), current == s))
            .ToList();

    private static List<SelectListItem> CloneSelected(List<SelectListItem> source, int? selectedId)
    {
        var value = selectedId?.ToString();
        return source
            .Select(i => new SelectListItem(i.Text, i.Value,
                string.Equals(i.Value, value, StringComparison.Ordinal)))
            .ToList();
    }
}
