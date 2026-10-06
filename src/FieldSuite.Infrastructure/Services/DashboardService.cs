using FieldSuite.Domain.Common;
using FieldSuite.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FieldSuite.Infrastructure.Services;

public record DashboardStats(
    int ActiveProjects,
    int OpenIncidents,
    int OverdueActions,
    int ActivePermits,
    int PendingPermits,
    int OpenSnags,
    int OpenNCRs,
    int WorkersPresentToday,
    int TotalActiveWorkers,
    int ExpiringDocuments,
    int SentEstimates);

public interface IDashboardService
{
    Task<DashboardStats> GetAsync(int organizationId);
}

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    public DashboardService(AppDbContext db) => _db = db;

    public async Task<DashboardStats> GetAsync(int organizationId)
    {
        var today = DateTime.UtcNow.Date;

        var activeProjects = await _db.Projects.CountAsync(p => p.OrganizationId == organizationId && p.IsActive);
        var openIncidents = await _db.Incidents.CountAsync(i => i.OrganizationId == organizationId && i.Status < IncidentStatus.Resolved);
        var overdueActions = await _db.CorrectiveActions.CountAsync(c =>
            c.Status != CorrectiveActionStatus.Done && c.Status != CorrectiveActionStatus.Overdue && c.DueDate < DateTime.UtcNow
            && c.Incident!.OrganizationId == organizationId);
        var activePermits = await _db.WorkPermits.CountAsync(p => p.OrganizationId == organizationId && p.Status == PermitStatus.Active);
        var pendingPermits = await _db.WorkPermits.CountAsync(p => p.OrganizationId == organizationId && p.Status == PermitStatus.PendingApproval);
        var openSnags = await _db.Snags.CountAsync(s =>
            s.Project!.OrganizationId == organizationId && s.Status != SnagStatus.Closed && s.Status != SnagStatus.Rejected);
        var openNCRs = await _db.NCRs.CountAsync(n => n.Project!.OrganizationId == organizationId && n.Status != NCRStatus.Closed);
        var workersPresentToday = await _db.Attendances.CountAsync(a =>
            a.Worker!.OrganizationId == organizationId && a.WorkDate == today && a.Status == AttendanceStatus.Present);
        var totalActiveWorkers = await _db.Workers.CountAsync(w => w.OrganizationId == organizationId && w.IsActive);
        var expiringDocuments = await _db.OnboardingDocuments.CountAsync(d =>
            d.ExpiryDate != null && d.ExpiryDate <= today.AddDays(30) && d.Worker!.OrganizationId == organizationId);
        var sentEstimates = await _db.Estimates.CountAsync(e => e.OrganizationId == organizationId && e.Status == EstimateStatus.Sent);

        return new DashboardStats(activeProjects, openIncidents, overdueActions, activePermits, pendingPermits,
            openSnags, openNCRs, workersPresentToday, totalActiveWorkers, expiringDocuments, sentEstimates);
    }
}
