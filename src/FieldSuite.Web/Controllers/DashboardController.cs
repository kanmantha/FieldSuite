using FieldSuite.Domain.Common;
using FieldSuite.Domain.Entities;
using FieldSuite.Infrastructure.Services;
using FieldSuite.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FieldSuite.Web.Controllers;

[Authorize]
public class DashboardController : AppController
{
    private readonly IDashboardService _dashboard;
    private readonly INudgeEngine _nudge;

    public DashboardController(AppDbContext db, ICurrentUser current, IAuditService audit,
        IDashboardService dashboard, INudgeEngine nudge) : base(db, current, audit)
    {
        _dashboard = dashboard;
        _nudge = nudge;
    }

    public async Task<IActionResult> Index()
    {
        var stats = await _dashboard.GetAsync(OrgId);
        var nudges = await _nudge.ComputeAsync(OrgId);

        var recentIncidents = await Db.Incidents
            .Where(i => i.OrganizationId == OrgId)
            .OrderByDescending(i => i.OccurredAt)
            .Take(5)
            .Include(i => i.Project)
            .ToListAsync();

        var upcomingPermits = await Db.WorkPermits
            .Where(p => p.OrganizationId == OrgId && (p.Status == PermitStatus.PendingApproval || p.Status == PermitStatus.Active))
            .OrderBy(p => p.RequestedStart)
            .Take(5)
            .Include(p => p.Project)
            .ToListAsync();

        var urgentSnags = await Db.Snags
            .Where(s => s.Project!.OrganizationId == OrgId && s.Status != SnagStatus.Closed && s.Status != SnagStatus.Rejected)
            .OrderBy(s => s.DueDate == null)
            .ThenBy(s => s.DueDate)
            .Take(5)
            .Include(s => s.Project)
            .ToListAsync();

        ViewBag.Stats = stats;
        ViewBag.Nudges = nudges;
        ViewBag.RecentIncidents = recentIncidents;
        ViewBag.UpcomingPermits = upcomingPermits;
        ViewBag.UrgentSnags = urgentSnags;
        return View();
    }
}
