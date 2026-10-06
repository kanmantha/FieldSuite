using FieldSuite.Domain.Entities;

namespace FieldSuite.Infrastructure.Services;

public interface IAuditService
{
    Task SaveAsync(int organizationId, string userId, string userName, string action, string entityType, string entityId, string detail);
}

public interface INotificationService
{
    Task NotifyAsync(int organizationId, string userId, string message, string link);
    Task NotifyRoleAsync(int organizationId, string role, string message, string link);
    Task MarkReadAsync(int notificationId, string userId);
}
