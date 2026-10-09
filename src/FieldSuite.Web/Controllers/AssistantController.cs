using FieldSuite.Infrastructure.Data;
using FieldSuite.Infrastructure.Services;
using FieldSuite.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FieldSuite.Web.Controllers;

[Authorize]
public class AssistantController : AppController
{
    private readonly IAssistantService _assistant;
    private readonly IStructuredDraftService _drafts;

    public AssistantController(AppDbContext db, ICurrentUser current, IAuditService audit,
        IAssistantService assistant, IStructuredDraftService drafts) : base(db, current, audit)
    {
        _assistant = assistant;
        _drafts = drafts;
    }

    /// <summary>
    /// Renders AI Assistant page.
    /// </summary>
    public IActionResult Index() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask(string question)
    {
        var reply = await _assistant.AskAsync(OrgId, question);
        return Json(new { answer = reply.Answer, usedLlm = reply.UsedLlm });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Draft(string kind, string rawInput)
    {
        var draft = await _drafts.DraftAsync(kind, rawInput);
        return Json(new { draft.Title, draft.Description, draft.Category, draft.Severity, usedLlm = _drafts.IsLlmAvailable });
    }
}
