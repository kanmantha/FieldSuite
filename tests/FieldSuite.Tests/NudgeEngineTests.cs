using FieldSuite.Domain.Common;
using FieldSuite.Domain.Entities;
using FieldSuite.Infrastructure.Data;
using FieldSuite.Infrastructure.Services;

namespace FieldSuite.Tests;

public class NudgeEngineTests
{
    private static async Task<AppDbContext> CreateDbAsync()
    {
        var db = TestDb.Create();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task ExpireStalePermits_OnlyAffectsExpiredPermitsInSameOrg()
    {
        await using var db = await CreateDbAsync();
        var past = DateTime.UtcNow.AddHours(-2);
        db.WorkPermits.AddRange(
            new WorkPermit { OrganizationId = 1, ProjectId = 1, Reference = "PWT-0001", Title = "Old", RequestedById = "u", RequestedStart = past.AddDays(-1), RequestedEnd = past, Status = PermitStatus.Approved },
            new WorkPermit { OrganizationId = 1, ProjectId = 1, Reference = "PWT-0002", Title = "Current", RequestedById = "u", RequestedStart = past, RequestedEnd = past.AddHours(6), Status = PermitStatus.Active },
            new WorkPermit { OrganizationId = 2, ProjectId = 2, Reference = "PWT-0003", Title = "Foreign old", RequestedById = "u", RequestedStart = past.AddDays(-1), RequestedEnd = past, Status = PermitStatus.Approved });
        await db.SaveChangesAsync();

        var engine = new NudgeEngine(db);
        var expired = await engine.ExpireStalePermitsAsync(1);

        Assert.Equal(1, expired);
        Assert.Equal(PermitStatus.Expired, db.WorkPermits.Single(p => p.Reference == "PWT-0001").Status);
        Assert.Equal(PermitStatus.Active, db.WorkPermits.Single(p => p.Reference == "PWT-0002").Status);
        Assert.Equal(PermitStatus.Approved, db.WorkPermits.Single(p => p.Reference == "PWT-0003").Status);
    }

    [Fact]
    public async Task ComputeAsync_FlagsOverdueCorrectiveActionAndBuildsNudge()
    {
        await using var db = await CreateDbAsync();
        var incident = new Incident { OrganizationId = 1, ProjectId = 1, Reference = "SIN-0001", Title = "Trip", Status = IncidentStatus.Investigating, OccurredAt = DateTime.UtcNow.AddDays(-1) };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();
        db.CorrectiveActions.Add(new CorrectiveAction { IncidentId = incident.Id, Description = "Repair the cable", DueDate = DateTime.UtcNow.AddDays(-3), Status = CorrectiveActionStatus.Open });
        await db.SaveChangesAsync();

        var engine = new NudgeEngine(db);
        var nudges = await engine.ComputeAsync(1);

        Assert.Contains(nudges, n => n.Kind == "Corrective action" && n.Severity == "danger");
        Assert.Equal(CorrectiveActionStatus.Overdue, db.CorrectiveActions.Single().Status);
    }

    [Fact]
    public async Task ComputeAsync_NudgesOverdueSnag_AndIgnoresClosed()
    {
        await using var db = await CreateDbAsync();
        db.Projects.AddRange(
            new Project { OrganizationId = 1, Name = "Mall", Code = "M1" },
            new Project { OrganizationId = 2, Name = "Other", Code = "O1" });
        await db.SaveChangesAsync();
        var p1 = db.Projects.Single(p => p.Code == "M1");
        var p2 = db.Projects.Single(p => p.Code == "O1");
        db.Snags.AddRange(
            new Snag { ProjectId = p1.Id, Reference = "SNG-0001", Title = "Open overdue", Status = SnagStatus.Open, DueDate = DateTime.UtcNow.AddDays(-2) },
            new Snag { ProjectId = p1.Id, Reference = "SNG-0002", Title = "Closed", Status = SnagStatus.Closed, DueDate = DateTime.UtcNow.AddDays(-2) },
            new Snag { ProjectId = p2.Id, Reference = "SNG-0003", Title = "Foreign overdue", Status = SnagStatus.Open, DueDate = DateTime.UtcNow.AddDays(-2) });
        await db.SaveChangesAsync();

        var engine = new NudgeEngine(db);
        var nudges = await engine.ComputeAsync(1);

        var snagNudges = nudges.Where(n => n.Kind == "Snag").ToList();
        Assert.Single(snagNudges);
        Assert.Contains("SNG-0001", snagNudges[0].Message);
        Assert.Equal("danger", snagNudges[0].Severity);
    }

    [Fact]
    public async Task ComputeAsync_NudgesExpiringPermitWithinTwoHours()
    {
        await using var db = await CreateDbAsync();
        db.WorkPermits.Add(new WorkPermit
        {
            OrganizationId = 1,
            ProjectId = 1,
            Reference = "PWT-0010",
            Title = "Hot work",
            RequestedById = "u",
            RequestedStart = DateTime.UtcNow,
            RequestedEnd = DateTime.UtcNow.AddHours(1),
            Status = PermitStatus.Active
        });
        await db.SaveChangesAsync();

        var engine = new NudgeEngine(db);
        var nudges = await engine.ComputeAsync(1);

        Assert.Contains(nudges, n => n.Kind == "Permit" && n.Message.Contains("PWT-0010"));
    }
}
