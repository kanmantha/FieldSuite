using FieldSuite.Domain.Common;
using FieldSuite.Domain.Entities;
using FieldSuite.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FieldSuite.Web.Controllers;

[Authorize]
public class ProjectsController : AppController
{
    public ProjectsController(AppDbContext db, ICurrentUser current, Infrastructure.Services.IAuditService audit)
        : base(db, current, audit) { }

    public async Task<IActionResult> Index()
    {
        var projects = await Db.Projects
            .Where(p => p.OrganizationId == OrgId)
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.Name)
            .Select(p => new
            {
                Project = p,
                OpenSnags = Db.Snags.Count(s => s.ProjectId == p.Id && s.Status != SnagStatus.Closed && s.Status != SnagStatus.Rejected),
                OpenIncidents = Db.Incidents.Count(i => i.ProjectId == p.Id && i.Status < IncidentStatus.Resolved),
                ActivePermits = Db.WorkPermits.Count(w => w.ProjectId == p.Id && w.Status == PermitStatus.Active)
            })
            .ToListAsync();

        ViewBag.ProjectStats = projects.Select(x => new ProjectRowStats(x.Project, x.OpenSnags, x.OpenIncidents, x.ActivePermits)).ToList();
        return View(projects.Select(x => x.Project).ToList());
    }

    public record ProjectRowStats(Project Project, int OpenSnags, int OpenIncidents, int ActivePermits);

    [HttpGet]
    public IActionResult Create() => View(new Project { StartDate = DateTime.UtcNow.Date });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Project model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Project name is required.");
        if (string.IsNullOrWhiteSpace(model.Code))
            ModelState.AddModelError(nameof(model.Code), "Project code is required.");
        else if (await Db.Projects.AnyAsync(p => p.OrganizationId == OrgId && p.Code == model.Code))
            ModelState.AddModelError(nameof(model.Code), "This code is already in use.");

        if (!ModelState.IsValid) return View(model);

        model.OrganizationId = OrgId;
        model.IsActive = model.Status == ProjectStatus.Active;
        Db.Projects.Add(model);
        await Db.SaveChangesAsync();
        await AuditAsync("Create", "Project", model.Id, $"Created project {model.Code} - {model.Name}");
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == OrgId);
        if (project is null) return NotFound();

        ViewBag.OpenSnags = await Db.Snags.CountAsync(s => s.ProjectId == id && s.Status != SnagStatus.Closed && s.Status != SnagStatus.Rejected);
        ViewBag.OpenIncidents = await Db.Incidents.CountAsync(i => i.ProjectId == id && i.Status < IncidentStatus.Resolved);
        ViewBag.ActivePermits = await Db.WorkPermits.CountAsync(w => w.ProjectId == id && (w.Status == PermitStatus.Active || w.Status == PermitStatus.PendingApproval));
        ViewBag.TodayAttendance = await Db.Attendances.CountAsync(a => a.ProjectId == id && a.WorkDate == DateTime.UtcNow.Date);
        return View(project);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == OrgId);
        if (project is null) return NotFound();
        return View(project);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Project model)
    {
        var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == OrgId);
        if (project is null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Project name is required.");

        if (!ModelState.IsValid) return View(model);

        project.Name = model.Name;
        project.Code = model.Code;
        project.ClientName = model.ClientName;
        project.Address = model.Address;
        project.StartDate = model.StartDate;
        project.EndDate = model.EndDate;
        project.Status = model.Status;
        project.IsActive = model.Status == ProjectStatus.Active;
        await Db.SaveChangesAsync();
        await AuditAsync("Update", "Project", project.Id, $"Updated project {project.Code}");
        return RedirectToAction(nameof(Details), new { id });
    }
}
