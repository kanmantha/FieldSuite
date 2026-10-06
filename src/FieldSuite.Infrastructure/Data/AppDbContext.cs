using FieldSuite.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FieldSuite.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<CorrectiveAction> CorrectiveActions => Set<CorrectiveAction>();
    public DbSet<ToolboxTalk> ToolboxTalks => Set<ToolboxTalk>();
    public DbSet<ToolboxAttendance> ToolboxAttendances => Set<ToolboxAttendance>();

    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<InspectionItem> InspectionItems => Set<InspectionItem>();
    public DbSet<Snag> Snags => Set<Snag>();
    public DbSet<NCR> NCRs => Set<NCR>();

    public DbSet<WorkPermit> WorkPermits => Set<WorkPermit>();
    public DbSet<PermitChecklistItem> PermitChecklistItems => Set<PermitChecklistItem>();
    public DbSet<PermitApproval> PermitApprovals => Set<PermitApproval>();

    public DbSet<Estimate> Estimates => Set<Estimate>();
    public DbSet<EstimateCategory> EstimateCategories => Set<EstimateCategory>();
    public DbSet<EstimateItem> EstimateItems => Set<EstimateItem>();

    public DbSet<ContractorCompany> ContractorCompanies => Set<ContractorCompany>();
    public DbSet<Worker> Workers => Set<Worker>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<OnboardingDocument> OnboardingDocuments => Set<OnboardingDocument>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(e =>
        {
            e.Property(u => u.FullName).HasMaxLength(200);
            e.HasIndex(u => u.OrganizationId);
        });

        builder.Entity<Organization>(e => e.Property(o => o.Name).HasMaxLength(200));

        builder.Entity<Project>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.Code).HasMaxLength(50);
            e.Property(p => p.ClientName).HasMaxLength(200);
            e.Property(p => p.Address).HasMaxLength(400);
            e.HasIndex(p => new { p.OrganizationId, p.Code }).IsUnique();
            e.HasMany(p => p.Incidents).WithOne(i => i.Project).HasForeignKey(i => i.ProjectId);
            e.HasMany(p => p.Snags).WithOne(s => s.Project).HasForeignKey(s => s.ProjectId);
            e.HasMany(p => p.WorkPermits).WithOne(w => w.Project).HasForeignKey(w => w.ProjectId);
            e.HasMany(p => p.Attendances).WithOne(a => a.Project).HasForeignKey(a => a.ProjectId);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.Property(a => a.Action).HasMaxLength(100);
            e.Property(a => a.EntityType).HasMaxLength(100);
            e.Property(a => a.EntityId).HasMaxLength(50);
            e.Property(a => a.UserName).HasMaxLength(200);
            e.HasIndex(a => a.OrganizationId);
        });

        builder.Entity<Notification>(e =>
        {
            e.Property(n => n.Message).HasMaxLength(500);
            e.Property(n => n.Link).HasMaxLength(500);
            e.HasIndex(n => new { n.UserId, n.IsRead });
        });

        builder.Entity<Incident>(e =>
        {
            e.Property(i => i.Reference).HasMaxLength(30);
            e.Property(i => i.Title).HasMaxLength(300);
            e.Property(i => i.Location).HasMaxLength(300);
            e.Property(i => i.RootCause).HasMaxLength(500);
            e.HasIndex(i => new { i.OrganizationId, i.Reference }).IsUnique();
        });

        builder.Entity<CorrectiveAction>(e => e.Property(c => c.Description).HasMaxLength(500));

        builder.Entity<ToolboxTalk>(e =>
        {
            e.Property(t => t.Topic).HasMaxLength(300);
            e.Property(t => t.Notes).HasMaxLength(2000);
        });

        builder.Entity<Inspection>(e =>
        {
            e.Property(i => i.Reference).HasMaxLength(30);
            e.Property(i => i.Title).HasMaxLength(300);
            e.Property(i => i.OverallNotes).HasMaxLength(2000);
            e.HasIndex(i => new { i.ProjectId, i.Reference }).IsUnique();
        });

        builder.Entity<Snag>(e =>
        {
            e.Property(s => s.Reference).HasMaxLength(30);
            e.Property(s => s.Title).HasMaxLength(300);
            e.Property(s => s.Description).HasMaxLength(2000);
            e.Property(s => s.Location).HasMaxLength(300);
            e.Property(s => s.PhotoPath).HasMaxLength(500);
            e.HasIndex(s => new { s.ProjectId, s.Reference }).IsUnique();
        });

        builder.Entity<NCR>(e =>
        {
            e.Property(n => n.Reference).HasMaxLength(30);
            e.Property(n => n.Title).HasMaxLength(300);
            e.Property(n => n.Description).HasMaxLength(2000);
            e.Property(n => n.ContainmentAction).HasMaxLength(2000);
            e.HasIndex(n => new { n.ProjectId, n.Reference }).IsUnique();
        });

        builder.Entity<WorkPermit>(e =>
        {
            e.Property(w => w.Reference).HasMaxLength(30);
            e.Property(w => w.Title).HasMaxLength(300);
            e.Property(w => w.Description).HasMaxLength(2000);
            e.Property(w => w.Location).HasMaxLength(300);
            e.Property(w => w.SpecialConditions).HasMaxLength(2000);
            e.HasIndex(w => new { w.OrganizationId, w.Reference }).IsUnique();
            e.HasIndex(w => w.Status);
        });

        builder.Entity<PermitChecklistItem>(e => e.Property(c => c.Text).HasMaxLength(500));
        builder.Entity<PermitApproval>(e => e.Property(a => a.ApproverRole).HasMaxLength(100));

        builder.Entity<Estimate>(e =>
        {
            e.Property(x => x.Reference).HasMaxLength(30);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.ClientName).HasMaxLength(300);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.MarginPercent).HasPrecision(5, 2);
            e.HasIndex(x => new { x.OrganizationId, x.Reference }).IsUnique();
        });

        builder.Entity<EstimateCategory>(e => e.Property(c => c.Name).HasMaxLength(200));

        builder.Entity<EstimateItem>(e =>
        {
            e.Property(i => i.Description).HasMaxLength(500);
            e.Property(i => i.Unit).HasMaxLength(30);
            e.Property(i => i.Quantity).HasPrecision(18, 2);
            e.Property(i => i.UnitRate).HasPrecision(18, 2);
            e.Property(i => i.WastePercent).HasPrecision(5, 2);
            e.Ignore(i => i.LineTotal);
        });

        builder.Entity<ContractorCompany>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(300);
            e.Property(c => c.Trade).HasMaxLength(200);
            e.HasIndex(c => c.OrganizationId);
        });

        builder.Entity<Worker>(e =>
        {
            e.Property(w => w.FullName).HasMaxLength(200);
            e.Property(w => w.JobTitle).HasMaxLength(200);
            e.HasIndex(w => w.OrganizationId);
        });

        builder.Entity<Attendance>(e =>
        {
            e.HasIndex(a => new { a.WorkerId, a.WorkDate });
            e.Property(a => a.HoursWorked).HasPrecision(5, 2);
        });

        builder.Entity<OnboardingDocument>(e =>
        {
            e.Property(d => d.DocumentNumber).HasMaxLength(100);
            e.Property(d => d.FilePath).HasMaxLength(500);
        });

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(new UtcDateTimeConverter());
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(new UtcNullableDateTimeConverter());
            }
        }
    }
}

public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        v => v.Kind == DateTimeKind.Utc ? v
           : v.Kind == DateTimeKind.Local ? v.ToUniversalTime()
           : DateTime.SpecifyKind(v, DateTimeKind.Utc),
        v => v)
    {
    }
}

public class UtcNullableDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public UtcNullableDateTimeConverter() : base(
        v => v == null ? v
           : v.Value.Kind == DateTimeKind.Utc ? v.Value
           : v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime()
           : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc),
        v => v)
    {
    }
}
