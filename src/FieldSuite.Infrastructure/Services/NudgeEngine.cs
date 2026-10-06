using FieldSuite.Domain.Common;
using FieldSuite.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FieldSuite.Infrastructure.Services;

public record Nudge(string Kind, string Severity, string Message, string Link);

public interface INudgeEngine
{
    Task<List<Nudge>> ComputeAsync(int organizationId);
    Task<int> ExpireStalePermitsAsync(int organizationId);
}

public class NudgeEngine : INudgeEngine
{
    private readonly AppDbContext _db;
    public NudgeEngine(AppDbContext db) => _db = db;

    public async Task<int> ExpireStalePermitsAsync(int organizationId)
    {
        var now = DateTime.UtcNow;
        var stale = await _db.WorkPermits
            .Where(p => p.OrganizationId == organizationId)
            .Where(p => (p.Status == PermitStatus.Approved || p.Status == PermitStatus.Active || p.Status == PermitStatus.PendingApproval)
                        && p.RequestedEnd < now)
            .ToListAsync();

        foreach (var permit in stale)
        {
            if (permit.Status == PermitStatus.Active) permit.ActualEnd = now;
            permit.Status = PermitStatus.Expired;
        }
        if (stale.Count > 0) await _db.SaveChangesAsync();
        return stale.Count;
    }

    public async Task<List<Nudge>> ComputeAsync(int organizationId)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var nudges = new List<Nudge>();

        await ExpireStalePermitsAsync(organizationId);

        var overdueActions = await _db.CorrectiveActions
            .Where(c => c.Status != CorrectiveActionStatus.Done && c.Status != CorrectiveActionStatus.Overdue && c.DueDate < now)
            .Where(c => c.Incident!.OrganizationId == organizationId)
            .Include(c => c.Incident)
            .Take(20)
            .ToListAsync();
        foreach (var action in overdueActions)
        {
            action.Status = CorrectiveActionStatus.Overdue;
            nudges.Add(new Nudge("Corrective action", "danger",
                $"Overdue corrective action: {action.Description} (incident {action.Incident!.Reference})",
                $"/Safety/IncidentDetails/{action.IncidentId}"));
        }
        if (overdueActions.Count > 0) await _db.SaveChangesAsync();

        var expiringPermits = await _db.WorkPermits
            .Where(p => p.OrganizationId == organizationId
                        && (p.Status == PermitStatus.Approved || p.Status == PermitStatus.Active)
                        && p.RequestedEnd > now && p.RequestedEnd <= now.AddHours(2))
            .Take(20)
            .ToListAsync();
        foreach (var permit in expiringPermits)
            nudges.Add(new Nudge("Permit", "warning",
                $"Permit {permit.Reference} expires at {permit.RequestedEnd:HH:mm} UTC",
                $"/Permits/Details/{permit.Id}"));

        var pendingHighRisk = await _db.WorkPermits
            .Where(p => p.OrganizationId == organizationId && p.Status == PermitStatus.PendingApproval && p.RiskLevel >= RiskLevel.High)
            .Take(20)
            .ToListAsync();
        foreach (var permit in pendingHighRisk)
            nudges.Add(new Nudge("Permit approval", "warning",
                $"High-risk permit {permit.Reference} awaiting approval",
                $"/Permits/Details/{permit.Id}"));

        var expiringDocs = await _db.OnboardingDocuments
            .Where(d => d.ExpiryDate != null && d.ExpiryDate <= today.AddDays(30))
            .Where(d => d.Worker!.OrganizationId == organizationId)
            .Include(d => d.Worker)
            .Take(20)
            .ToListAsync();
        foreach (var doc in expiringDocs)
            nudges.Add(new Nudge("Document", "warning",
                $"{doc.Type} for {doc.Worker!.FullName} expires {doc.ExpiryDate:dd MMM yyyy}",
                $"/Workforce/WorkerDetails/{doc.WorkerId}"));

        var agingSnags = await _db.Snags
            .Where(s => s.Project!.OrganizationId == organizationId
                        && s.Status != SnagStatus.Closed && s.Status != SnagStatus.Rejected
                        && s.DueDate != null && s.DueDate < today)
            .Take(20)
            .ToListAsync();
        foreach (var snag in agingSnags)
            nudges.Add(new Nudge("Snag", "danger",
                $"Snag {snag.Reference} overdue: {snag.Title}",
                $"/Quality/SnagDetails/{snag.Id}"));

        var staleIncidents = await _db.Incidents
            .Where(i => i.OrganizationId == organizationId
                        && (i.Status == IncidentStatus.Reported || i.Status == IncidentStatus.Investigating)
                        && i.OccurredAt < now.AddDays(-7))
            .Take(20)
            .ToListAsync();
        foreach (var incident in staleIncidents)
            nudges.Add(new Nudge("Incident", "warning",
                $"Incident {incident.Reference} open for over 7 days",
                $"/Safety/IncidentDetails/{incident.Id}"));

        return nudges.OrderByDescending(n => n.Severity == "danger").ThenBy(n => n.Severity).ToList();
    }
}
