using System.Text;
using FieldSuite.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FieldSuite.Infrastructure.Services;

public record AssistantReply(string Answer, bool UsedLlm);

public interface IAssistantService
{
    Task<AssistantReply> AskAsync(int organizationId, string question);
}

public class AssistantService : IAssistantService
{
    private readonly AppDbContext _db;
    private readonly ILLMClient _llm;

    public AssistantService(AppDbContext db, ILLMClient llm)
    {
        _db = db;
        _llm = llm;
    }

    /// <summary>
    /// Answers question using rules engine or configured LLM if available.
    /// </summary>
    public async Task<AssistantReply> AskAsync(int organizationId, string question)
    {
        var q = (question ?? string.Empty).Trim();
        if (q.Length == 0) return new AssistantReply("Ask me anything about your sites, permits, incidents, snags or workforce.", false);

        var stats = await BuildStatsAsync(organizationId);

        if (_llm.IsConfigured)
        {
            var system = "You are FieldSuite's field operations assistant for a construction SME. " +
                         "Answer concisely using ONLY the provided site statistics. If data is missing, say so. " +
                         "Statistics:\n" + stats;
            var answer = await _llm.CompleteAsync(system, q);
            if (!string.IsNullOrWhiteSpace(answer))
                return new AssistantReply(answer.Trim(), true);
        }

        return new AssistantReply(RuleBasedAnswer(q, organizationId), false);
    }

    private async Task<string> BuildStatsAsync(int organizationId)
    {
        var now = DateTime.UtcNow;
        var openIncidents = await _db.Incidents.CountAsync(i => i.OrganizationId == organizationId && i.Status < Domain.Common.IncidentStatus.Resolved);
        var activePermits = await _db.WorkPermits.CountAsync(p => p.OrganizationId == organizationId && p.Status == Domain.Common.PermitStatus.Active);
        var pendingPermits = await _db.WorkPermits.CountAsync(p => p.OrganizationId == organizationId && p.Status == Domain.Common.PermitStatus.PendingApproval);
        var openSnags = await _db.Snags.CountAsync(s => s.Project!.OrganizationId == organizationId && s.Status < Domain.Common.SnagStatus.Closed && s.Status != Domain.Common.SnagStatus.Rejected);
        var workersToday = await _db.Attendances.CountAsync(a => a.Worker!.OrganizationId == organizationId && a.WorkDate == now.Date && a.Status == Domain.Common.AttendanceStatus.Present);
        var activeProjects = await _db.Projects.CountAsync(p => p.OrganizationId == organizationId && p.IsActive);

        var sb = new StringBuilder();
        sb.AppendLine($"Active projects: {activeProjects}");
        sb.AppendLine($"Open incidents/near-misses: {openIncidents}");
        sb.AppendLine($"Active permits: {activePermits}; pending approval: {pendingPermits}");
        sb.AppendLine($"Open snags: {openSnags}");
        sb.AppendLine($"Workers present today: {workersToday}");
        return sb.ToString();
    }

    private string RuleBasedAnswer(string q, int organizationId)
    {
        var lower = q.ToLowerInvariant();
        if (lower.Contains("permit"))
            return "Ask for permit counts on the dashboard, or open Permits → Work Permits. " +
                   "High-risk permits need 3-level approval (Site Manager → Safety Officer → Admin). " +
                   "I can give live counts when connected to an LLM (configure Llm:BaseUrl in appsettings).";
        if (lower.Contains("incident") || lower.Contains("safety") || lower.Contains("near"))
            return "Open Safety → Incidents to review open incidents, near-misses and corrective actions. " +
                   "Overdue corrective actions surface as red nudges on the dashboard.";
        if (lower.Contains("snag") || lower.Contains("quality") || lower.Contains("punch"))
            return "Open Quality → Snags for the punch list. Overdue snags are flagged red on the dashboard.";
        if (lower.Contains("worker") || lower.Contains("attendance") || lower.Contains("staff"))
            return "Open Workforce → Attendance for today's site attendance, and Workers for onboarding documents expiring within 30 days.";
        if (lower.Contains("estimat") || lower.Contains("quote") || lower.Contains("boq") || lower.Contains("proposal"))
            return "Open Estimation → Estimates to build BOQ line items with waste %, margin and printable proposals. " +
                   "Unit-rate suggestions appear as you type based on your historical estimates.";

        return "Offline assistant mode: I answer questions about permits, incidents, snags, workforce and estimates. " +
               "Configure Llm:BaseUrl (OpenAI-compatible, e.g. Ollama) for full natural-language answers over your data.";
    }
}
