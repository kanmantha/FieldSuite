using FieldSuite.Domain.Entities;
using FieldSuite.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FieldSuite.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    public AuditService(AppDbContext db) => _db = db;

    /// <summary>
    /// Saves audit log entry.
    /// </summary>
    public async Task SaveAsync(int organizationId, string userId, string userName, string action, string entityType, string entityId, string detail)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            OrganizationId = organizationId,
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Detail = detail,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public NotificationService(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    /// <summary>
    /// Creates notification for specific user.
    /// </summary>
    public async Task NotifyAsync(int organizationId, string userId, string message, string link)
    {
        _db.Notifications.Add(new Notification
        {
            OrganizationId = organizationId,
            UserId = userId,
            Message = message.Length > 500 ? message[..500] : message,
            Link = link
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Creates notifications for all users in role.
    /// </summary>
    public async Task NotifyRoleAsync(int organizationId, string role, string message, string link)
    {
        var users = await _userManager.Users
            .Where(u => u.OrganizationId == organizationId)
            .ToListAsync();

        foreach (var user in users)
        {
            if (await _userManager.IsInRoleAsync(user, role))
            {
                _db.Notifications.Add(new Notification
                {
                    OrganizationId = organizationId,
                    UserId = user.Id,
                    Message = message.Length > 500 ? message[..500] : message,
                    Link = link
                });
            }
        }
        await _db.SaveChangesAsync();
    }

    public async Task MarkReadAsync(int notificationId, string userId)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
        if (n is null) return;
        n.IsRead = true;
        await _db.SaveChangesAsync();
    }
}
