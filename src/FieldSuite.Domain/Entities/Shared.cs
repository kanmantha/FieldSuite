using FieldSuite.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace FieldSuite.Domain.Entities;

public abstract class Entity
{
    public int Id { get; set; }
}

public class Organization : Entity
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}

public class AppUser : IdentityUser
{
    public int OrganizationId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public Organization? Organization { get; set; }
}

public class Project : Entity
{
    public int OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public bool IsActive { get; set; } = true;
    public Organization? Organization { get; set; }

    public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
    public ICollection<Snag> Snags { get; set; } = new List<Snag>();
    public ICollection<WorkPermit> WorkPermits { get; set; } = new List<WorkPermit>();
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}

public class AuditLog : Entity
{
    public int OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class Notification : Entity
{
    public int OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
