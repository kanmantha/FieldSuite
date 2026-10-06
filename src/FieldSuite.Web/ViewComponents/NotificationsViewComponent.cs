using FieldSuite.Infrastructure.Data;
using FieldSuite.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FieldSuite.Web.ViewComponents;

public class NotificationsViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public NotificationsViewComponent(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!_current.IsAuthenticated) return Content(string.Empty);

        var notifications = await _db.Notifications
            .Where(n => n.UserId == _current.UserId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(12)
            .ToListAsync();

        var unread = notifications.Count(n => !n.IsRead);
        ViewBag.UnreadCount = unread;
        return View(notifications);
    }
}
