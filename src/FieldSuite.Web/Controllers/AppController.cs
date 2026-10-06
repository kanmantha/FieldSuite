using FieldSuite.Infrastructure.Data;
using FieldSuite.Infrastructure.Services;
using FieldSuite.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace FieldSuite.Web.Controllers;

public abstract class AppController : Controller
{
    protected readonly AppDbContext Db;
    protected readonly ICurrentUser Current;
    protected readonly IAuditService Audit;

    protected AppController(AppDbContext db, ICurrentUser current, IAuditService audit)
    {
        Db = db;
        Current = current;
        Audit = audit;
    }

    protected int OrgId => Current.OrgId;

    protected async Task AuditAsync(string action, string entityType, object entityId, string detail) =>
        await Audit.SaveAsync(OrgId, Current.UserId, Current.FullName, action, entityType, entityId.ToString() ?? "0", detail);

    protected IActionResult Denied() => RedirectToAction("Index", "Dashboard");
}
