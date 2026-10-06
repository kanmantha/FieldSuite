using FieldSuite.Domain.Common;
using FieldSuite.Domain.Entities;
using FieldSuite.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FieldSuite.Infrastructure;

public static class DbInitializer
{
    public static readonly string[] Roles = { "Admin", "SiteManager", "SafetyOfficer", "QCInspector", "Estimator", "Worker" };

    private static string TempRef() => $"TMP-{Guid.NewGuid():N}"[..28];

    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        await db.Database.MigrateAsync();

        if (await db.Organizations.AnyAsync()) return;

        var hasher = new PasswordHasher<AppUser>();
        var org = new Organization { Name = "Verma Construction" };
        db.Organizations.Add(org);

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        await db.SaveChangesAsync();

        var users = new (string Email, string Name, string Role)[]
        {
            ("admin@demo.fieldsuite", "Arun Verma", "Admin"),
            ("site@demo.fieldsuite", "Ravi Kumar", "SiteManager"),
            ("safety@demo.fieldsuite", "Meena Iyer", "SafetyOfficer"),
            ("qc@demo.fieldsuite", "Suresh Patil", "QCInspector"),
            ("est@demo.fieldsuite", "Nisha Rao", "Estimator")
        };

        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var userMap = new Dictionary<string, AppUser>();
        foreach (var (email, name, role) in users)
        {
            var user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                OrganizationId = org.Id,
                FullName = name
            };
            user.PasswordHash = hasher.HashPassword(user, "Demo123!");
            var result = await userManager.CreateAsync(user);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(user, role);
            await userManager.AddClaimsAsync(user, new[]
            {
                new System.Security.Claims.Claim("org_id", org.Id.ToString()),
                new System.Security.Claims.Claim("full_name", name)
            });
            userMap[role] = user;
        }

        var projects = new[]
        {
            new Project { OrganizationId = org.Id, Name = "Riverside Mall Phase 2", Code = "RVP-02", ClientName = "Riverside Developers Ltd", Address = "Plot 14, Riverside Estate", StartDate = DateTime.UtcNow.Date.AddMonths(-4), EndDate = DateTime.UtcNow.Date.AddMonths(5), Status = ProjectStatus.Active },
            new Project { OrganizationId = org.Id, Name = "SteelFab Plant Expansion", Code = "SFP-01", ClientName = "SteelFab Industries", Address = "SEZ Unit 7, Industrial Lane", StartDate = DateTime.UtcNow.Date.AddMonths(-2), EndDate = DateTime.UtcNow.Date.AddMonths(8), Status = ProjectStatus.Active }
        };
        db.Projects.AddRange(projects);
        await db.SaveChangesAsync();

        await SeedWorkforce(db, org, projects);
        await SeedSafety(db, org, projects, userMap);
        await SeedQuality(db, org, projects, userMap);
        await SeedPermits(db, org, projects, userMap);
        await SeedEstimation(db, org, userMap);

        db.Notifications.AddRange(
            new Notification { OrganizationId = org.Id, UserId = userMap["SiteManager"].Id, Message = "Welcome to FieldSuite — demo data loaded.", Link = "/" },
            new Notification { OrganizationId = org.Id, UserId = userMap["SafetyOfficer"].Id, Message = "Check open incidents with overdue corrective actions.", Link = "/Safety" });

        await db.SaveChangesAsync();
        logger.LogInformation("Database seeded with demo data for organization '{Org}'.", org.Name);
    }

    private static async Task SeedWorkforce(AppDbContext db, Organization org, Project[] projects)
    {
        var contractor = new ContractorCompany
        {
            OrganizationId = org.Id,
            Name = "Apex Steel Fixers",
            ContactPerson = "Imran Shaikh",
            Phone = "+91 98100 11111",
            Email = "contact@apexsteel.example",
            Trade = "Steel Fixing"
        };
        db.ContractorCompanies.Add(contractor);
        await db.SaveChangesAsync();

        var workers = new[]
        {
            new Worker { OrganizationId = org.Id, FullName = "Ramesh Yadav", JobTitle = "Steel Fixer", Phone = "+91 98100 22221", Shift = ShiftType.Morning, ContractorCompanyId = contractor.Id },
            new Worker { OrganizationId = org.Id, FullName = "Sunil Kumar", JobTitle = "Welder", Phone = "+91 98100 22222", Shift = ShiftType.Day },
            new Worker { OrganizationId = org.Id, FullName = "Farhan Ali", JobTitle = "Rigger", Phone = "+91 98100 22223", Shift = ShiftType.Morning, ContractorCompanyId = contractor.Id },
            new Worker { OrganizationId = org.Id, FullName = "Devendra Pawar", JobTitle = "Scaffolders", Phone = "+91 98100 22224", Shift = ShiftType.Night },
            new Worker { OrganizationId = org.Id, FullName = "Kavita Sharma", JobTitle = "Site Supervisor", Phone = "+91 98100 22225", Shift = ShiftType.Day },
            new Worker { OrganizationId = org.Id, FullName = "Joseph Mathew", JobTitle = "Crane Operator", Phone = "+91 98100 22226", Shift = ShiftType.Day }
        };
        db.Workers.AddRange(workers);
        await db.SaveChangesAsync();

        db.OnboardingDocuments.AddRange(
            new OnboardingDocument { WorkerId = workers[0].Id, Type = WorkerDocumentType.SafetyCert, DocumentNumber = "SC-8841", IssuedDate = DateTime.UtcNow.Date.AddMonths(-8), ExpiryDate = DateTime.UtcNow.Date.AddDays(22) },
            new OnboardingDocument { WorkerId = workers[0].Id, Type = WorkerDocumentType.ID, DocumentNumber = "AADX1234Y" },
            new OnboardingDocument { WorkerId = workers[1].Id, Type = WorkerDocumentType.SafetyCert, DocumentNumber = "SC-8842", IssuedDate = DateTime.UtcNow.Date.AddMonths(-14), ExpiryDate = DateTime.UtcNow.Date.AddDays(12) },
            new OnboardingDocument { WorkerId = workers[1].Id, Type = WorkerDocumentType.Contract, IssuedDate = DateTime.UtcNow.Date.AddMonths(-10) },
            new OnboardingDocument { WorkerId = workers[2].Id, Type = WorkerDocumentType.SafetyCert, DocumentNumber = "SC-8843", IssuedDate = DateTime.UtcNow.Date.AddMonths(-2), ExpiryDate = DateTime.UtcNow.Date.AddDays(150) },
            new OnboardingDocument { WorkerId = workers[3].Id, Type = WorkerDocumentType.Medical, DocumentNumber = "MED-2211", IssuedDate = DateTime.UtcNow.Date.AddMonths(-5), ExpiryDate = DateTime.UtcNow.Date.AddDays(25) });

        var today = DateTime.UtcNow.Date;
        for (var dayOffset = -4; dayOffset <= 0; dayOffset++)
        {
            var date = today.AddDays(dayOffset);
            for (var i = 0; i < workers.Length; i++)
            {
                var status = i == 4 && dayOffset == -2 ? AttendanceStatus.OnLeave
                    : i == 3 && dayOffset == 0 ? AttendanceStatus.Absent
                    : i == 2 && dayOffset == -1 ? AttendanceStatus.Late
                    : AttendanceStatus.Present;
                if (status == AttendanceStatus.Absent || status == AttendanceStatus.OnLeave)
                {
                    db.Attendances.Add(new Attendance { WorkerId = workers[i].Id, ProjectId = projects[0].Id, WorkDate = date, Status = status });
                    continue;
                }
                var clockIn = date.AddHours(status == AttendanceStatus.Late ? 10 : 8).AddMinutes(i * 3);
                var clockOut = date.AddHours(18).AddMinutes(i * 2);
                db.Attendances.Add(new Attendance
                {
                    WorkerId = workers[i].Id,
                    ProjectId = i % 2 == 0 ? projects[0].Id : projects[1].Id,
                    WorkDate = date,
                    ClockIn = clockIn,
                    ClockOut = dayOffset == 0 ? null : clockOut,
                    HoursWorked = dayOffset == 0 ? null : Math.Round((decimal)(clockOut - clockIn).TotalHours, 2),
                    Status = status
                });
            }
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedSafety(AppDbContext db, Organization org, Project[] projects, Dictionary<string, AppUser> users)
    {
        var incident = new Incident
        {
            Reference = TempRef(),
            OrganizationId = org.Id,
            ProjectId = projects[0].Id,
            Type = IncidentType.NearMiss,
            Severity = Severity.High,
            Title = "Steel beam swung loose over walkway",
            Description = "A steel beam shifted in the crane slings at level 2 and swung approximately 1m over the pedestrian walkway. No one was in the exclusion zone. Sling certification checked and valid.",
            OccurredAt = DateTime.UtcNow.AddDays(-6),
            Location = "Level 2, Grid C-D",
            RootCause = "Improper slinging angle; exclusion zone not enforced during lift",
            ReportedById = users["SafetyOfficer"].Id,
            Status = IncidentStatus.Investigating
        };
        db.Incidents.Add(incident);

        var incident2 = new Incident
        {
            Reference = TempRef(),
            OrganizationId = org.Id,
            ProjectId = projects[1].Id,
            Type = IncidentType.FirstAid,
            Severity = Severity.Medium,
            Title = "Hand laceration while cutting rebar",
            Description = "Worker sustained a shallow cut on left hand using a hacksaw. First aid administered at site clinic; no lost time.",
            OccurredAt = DateTime.UtcNow.AddDays(-2),
            Location = "Fabrication yard",
            RootCause = "Cut-resistant gloves worn beyond service life",
            ReportedById = users["SiteManager"].Id,
            Status = IncidentStatus.Reported
        };
        db.Incidents.Add(incident2);
        await db.SaveChangesAsync();

        incident.Reference = $"SIN-{incident.Id:D4}";
        incident2.Reference = $"SIN-{incident2.Id:D4}";

        db.CorrectiveActions.AddRange(
            new CorrectiveAction { IncidentId = incident.Id, Description = "Re-brief all riggers on slinging angles and lift plans", AssignedToId = users["SiteManager"].Id, DueDate = DateTime.UtcNow.Date.AddDays(2), Status = CorrectiveActionStatus.InProgress },
            new CorrectiveAction { IncidentId = incident.Id, Description = "Install physical barriers at walkway exclusion zone", AssignedToId = users["SiteManager"].Id, DueDate = DateTime.UtcNow.Date.AddDays(-3), Status = CorrectiveActionStatus.Open },
            new CorrectiveAction { IncidentId = incident2.Id, Description = "Replace all cut-resistant gloves older than 30 days", AssignedToId = users["SafetyOfficer"].Id, DueDate = DateTime.UtcNow.Date.AddDays(5), Status = CorrectiveActionStatus.Open });

        var talk = new ToolboxTalk
        {
            ProjectId = projects[0].Id,
            Topic = "Lifting operations & exclusion zones",
            TalkDate = DateTime.UtcNow.Date.AddDays(-6),
            DurationMinutes = 20,
            FacilitatorId = users["SafetyOfficer"].Id,
            Notes = "Covered slinging angles, tag lines, and walkway exclusion zones after near-miss SIN-0001."
        };
        db.ToolboxTalks.Add(talk);
        await db.SaveChangesAsync();

        var workerIds = await db.Workers.Where(w => w.OrganizationId == org.Id).Take(5).Select(w => w.Id).ToListAsync();
        foreach (var workerId in workerIds)
            db.ToolboxAttendances.Add(new ToolboxAttendance { ToolboxTalkId = talk.Id, WorkerId = workerId });

        await db.SaveChangesAsync();
    }

    private static async Task SeedQuality(AppDbContext db, Organization org, Project[] projects, Dictionary<string, AppUser> users)
    {
        var inspection = new Inspection
        {
            Reference = TempRef(),
            ProjectId = projects[0].Id,
            Type = InspectionType.Internal,
            Title = "Blockwork Level 1 - internal finish inspection",
            InspectionDate = DateTime.UtcNow.Date.AddDays(-3),
            InspectorId = users["QCInspector"].Id,
            Status = InspectionStatus.Failed,
            OverallNotes = "2 of 6 checks failed; snags raised."
        };
        db.Inspections.Add(inspection);
        await db.SaveChangesAsync();
        inspection.Reference = $"QIN-{inspection.Id:D4}";

        db.InspectionItems.AddRange(
            new InspectionItem { InspectionId = inspection.Id, ChecklistText = "Wall plumb within 5mm over 2m", Result = CheckResult.Fail, Comment = "Grid B wall out by 8mm" },
            new InspectionItem { InspectionId = inspection.Id, ChecklistText = "Mortar joint thickness 10mm ±2", Result = CheckResult.Pass },
            new InspectionItem { InspectionId = inspection.Id, ChecklistText = "Opening sizes match drawings", Result = CheckResult.Pass },
            new InspectionItem { InspectionId = inspection.Id, ChecklistText = "No visible cracks or efflorescence", Result = CheckResult.Fail, Comment = "Hairline crack above door opening D-12" },
            new InspectionItem { InspectionId = inspection.Id, ChecklistText = "DPC continuous and lapped", Result = CheckResult.Pass },
            new InspectionItem { InspectionId = inspection.Id, ChecklistText = "Fire stopping at service penetrations", Result = CheckResult.N_A, Comment = "Services not installed yet" });

        var snags = new[]
        {
            new Snag { Reference = TempRef(), ProjectId = projects[0].Id, Title = "Wall plumb out of tolerance at Grid B", Description = "Blockwork wall 8mm out of plumb over 2m at Grid B/3. Rework required before plastering.", Location = "Level 1, Grid B/3", Priority = SnagPriority.High, RaisedById = users["QCInspector"].Id, AssignedToId = users["SiteManager"].Id, DueDate = DateTime.UtcNow.Date.AddDays(-2), Status = SnagStatus.InProgress },
            new Snag { Reference = TempRef(), ProjectId = projects[0].Id, Title = "Hairline crack above door D-12", Description = "Hairline crack in blockwork above door opening D-12. Monitor and fill with flexible filler.", Location = "Level 1, Corridor 4", Priority = SnagPriority.Medium, RaisedById = users["QCInspector"].Id, AssignedToId = users["SiteManager"].Id, DueDate = DateTime.UtcNow.Date.AddDays(3), Status = SnagStatus.Assigned },
            new Snag { Reference = TempRef(), ProjectId = projects[0].Id, Title = "Paint touch-up at column C7", Description = "Scuffed paint finish on column C7 at lobby height. Touch up with matching batch.", Location = "Lobby, Column C7", Priority = SnagPriority.Low, RaisedById = users["QCInspector"].Id, DueDate = DateTime.UtcNow.Date.AddDays(7), Status = SnagStatus.Open },
            new Snag { Reference = TempRef(), ProjectId = projects[1].Id, Title = "Weld porosity at bracing joint B12", Description = "Visible porosity on fillet weld at bracing joint B12. Grind out and re-weld, then re-inspect.", Location = "Bay 12, bracing joint", Priority = SnagPriority.Critical, RaisedById = users["QCInspector"].Id, AssignedToId = users["SiteManager"].Id, DueDate = DateTime.UtcNow.Date.AddDays(1), Status = SnagStatus.Retest }
        };
        db.Snags.AddRange(snags);

        db.NCRs.Add(new NCR
        {
            Reference = TempRef(),
            ProjectId = projects[1].Id,
            Title = "Concrete strength shortfall in cube tests",
            Severity = Severity.High,
            Description = "Cube tests for pour P-14 returned 26 MPa against design 32 MPa. Core sampling required.",
            DetectedDate = DateTime.UtcNow.Date.AddDays(-9),
            ContainmentAction = "Pour loading restricted pending core test results.",
            Status = NCRStatus.Investigation
        });

        await db.SaveChangesAsync();
        foreach (var snag in snags)
            snag.Reference = $"SNG-{snag.Id:D4}";
        var ncr = await db.NCRs.FirstAsync(n => n.Title.StartsWith("Concrete strength"));
        ncr.Reference = $"NCR-{ncr.Id:D4}";
        await db.SaveChangesAsync();
    }

    private static async Task SeedPermits(AppDbContext db, Organization org, Project[] projects, Dictionary<string, AppUser> users)
    {
        var permits = new[]
        {
            new WorkPermit
            {
                Reference = TempRef(),
                OrganizationId = org.Id, ProjectId = projects[0].Id,
                Type = PermitType.HotWork, Title = "Hot work - steel welding at roof deck",
                Description = "MIG welding of roof deck connections, zone R-4.", Location = "Roof deck, Zone R-4",
                RequestedById = users["SiteManager"].Id,
                RequestedStart = DateTime.UtcNow.Date.AddDays(1).AddHours(9),
                RequestedEnd = DateTime.UtcNow.Date.AddDays(1).AddHours(17),
                IsolationVerified = true, GasTestRequired = true, Status = PermitStatus.PendingApproval,
                SpecialConditions = "Fire watch 60 min after completion; 2 x 9kg extinguishers at work face."
            },
            new WorkPermit
            {
                Reference = TempRef(),
                OrganizationId = org.Id, ProjectId = projects[1].Id,
                Type = PermitType.ConfinedSpace, Title = "Confined space entry - storage tank T-201",
                Description = "Internal inspection of tank T-201 with forced ventilation.", Location = "Tank farm, T-201",
                RequestedById = users["SafetyOfficer"].Id,
                RequestedStart = DateTime.UtcNow.Date.AddDays(3).AddHours(8),
                RequestedEnd = DateTime.UtcNow.Date.AddDays(3).AddHours(16),
                IsolationVerified = false, GasTestRequired = true, Status = PermitStatus.Draft,
                SpecialConditions = "Attendant stationed at hatch; retrieval winch rigged."
            },
            new WorkPermit
            {
                Reference = TempRef(),
                OrganizationId = org.Id, ProjectId = projects[0].Id,
                Type = PermitType.Height, Title = "Work at height - facade bracket installation",
                Description = "Bracket installation from mobile scaffold, elevation B.", Location = "Elevation B, Level 3-4",
                RequestedById = users["SiteManager"].Id,
                RequestedStart = DateTime.UtcNow.AddHours(-20),
                RequestedEnd = DateTime.UtcNow.AddHours(3),
                IsolationVerified = true, GasTestRequired = false, Status = PermitStatus.Active,
                ActualStart = DateTime.UtcNow.AddHours(-18),
                SpecialConditions = "Harness + double lanyard; scaffold handover tag green."
            }
        };
        db.WorkPermits.AddRange(permits);
        await db.SaveChangesAsync();
        foreach (var permit in permits)
            permit.Reference = $"PWT-{permit.Id:D4}";

        var hotWork = permits[0];
        db.PermitChecklistItems.AddRange(
            new PermitChecklistItem { WorkPermitId = hotWork.Id, Text = "Area cleared of combustibles within 11m", IsChecked = true, CheckedById = users["SiteManager"].Id, CheckedAt = DateTime.UtcNow.AddHours(-1) },
            new PermitChecklistItem { WorkPermitId = hotWork.Id, Text = "Extinguishers staged at work face", IsChecked = true, CheckedById = users["SiteManager"].Id, CheckedAt = DateTime.UtcNow.AddHours(-1) },
            new PermitChecklistItem { WorkPermitId = hotWork.Id, Text = "Gas test completed and recorded", IsChecked = false },
            new PermitChecklistItem { WorkPermitId = hotWork.Id, Text = "Fire watch briefed and assigned", IsChecked = false });

        db.PermitApprovals.AddRange(
            new PermitApproval { WorkPermitId = hotWork.Id, Level = 1, ApproverRole = "SiteManager" },
            new PermitApproval { WorkPermitId = hotWork.Id, Level = 2, ApproverRole = "SafetyOfficer" },
            new PermitApproval { WorkPermitId = hotWork.Id, Level = 3, ApproverRole = "Admin" });

        var activePermit = permits[2];
        db.PermitApprovals.AddRange(
            new PermitApproval { WorkPermitId = activePermit.Id, Level = 1, ApproverRole = "SiteManager", Decision = ApprovalDecision.Approved, ApproverId = users["SiteManager"].Id, Comment = "Scaffold tag verified.", DecidedAt = DateTime.UtcNow.AddDays(-1) },
            new PermitApproval { WorkPermitId = activePermit.Id, Level = 2, ApproverRole = "SafetyOfficer", Decision = ApprovalDecision.Approved, ApproverId = users["SafetyOfficer"].Id, Comment = "Harness inspection valid.", DecidedAt = DateTime.UtcNow.AddDays(-1) },
            new PermitApproval { WorkPermitId = activePermit.Id, Level = 3, ApproverRole = "Admin", Decision = ApprovalDecision.Approved, ApproverId = users["Admin"].Id, Comment = "Approved.", DecidedAt = DateTime.UtcNow.AddDays(-1) });

        await db.SaveChangesAsync();
    }

    private static async Task SeedEstimation(AppDbContext db, Organization org, Dictionary<string, AppUser> users)
    {
        var estimate = new Estimate
        {
            Reference = TempRef(),
            OrganizationId = org.Id,
            Title = "Roofing & waterproofing package - Riverside Mall",
            ClientName = "Riverside Developers Ltd",
            Status = EstimateStatus.Draft,
            ValidUntil = DateTime.UtcNow.Date.AddDays(30),
            MarginPercent = 12,
            Notes = "Rates based on Q2 2026 procurement prices. Excludes VAT.",
            CreatedById = users["Estimator"].Id
        };
        db.Estimates.Add(estimate);
        await db.SaveChangesAsync();
        estimate.Reference = $"EST-{estimate.Id:D4}";

        var civil = new EstimateCategory { EstimateId = estimate.Id, Name = "Civil & Structural", SortOrder = 1 };
        var mep = new EstimateCategory { EstimateId = estimate.Id, Name = "Waterproofing & Finishes", SortOrder = 2 };
        db.EstimateCategories.AddRange(civil, mep);
        await db.SaveChangesAsync();

        db.EstimateItems.AddRange(
            new EstimateItem { EstimateCategoryId = civil.Id, Description = "Reinforced concrete slab 150mm", Quantity = 1250, Unit = "m2", UnitRate = 78, WastePercent = 3 },
            new EstimateItem { EstimateCategoryId = civil.Id, Description = "Blockwork 200mm single leaf", Quantity = 640, Unit = "m2", UnitRate = 34, WastePercent = 5 },
            new EstimateItem { EstimateCategoryId = civil.Id, Description = "Structural steel sections (supply & fix)", Quantity = 42, Unit = "ton", UnitRate = 1450, WastePercent = 2 },
            new EstimateItem { EstimateCategoryId = mep.Id, Description = "APP waterproofing membrane", Quantity = 980, Unit = "m2", UnitRate = 22, WastePercent = 8 },
            new EstimateItem { EstimateCategoryId = mep.Id, Description = "SBS torch-on membrane", Quantity = 320, Unit = "m2", UnitRate = 31, WastePercent = 8 },
            new EstimateItem { EstimateCategoryId = mep.Id, Description = "Ceramic floor tiles 600x600", Quantity = 540, Unit = "m2", UnitRate = 46, WastePercent = 10 });

        await db.SaveChangesAsync();
    }
}
