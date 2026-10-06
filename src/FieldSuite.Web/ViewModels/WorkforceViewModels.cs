using FieldSuite.Domain.Common;
using FieldSuite.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FieldSuite.Web.ViewModels;

public class AttendanceBoardRow
{
    public Worker Worker { get; set; } = new();
    public Attendance? Today { get; set; }
    public List<SelectListItem> StatusOptions { get; set; } = new();
    public List<SelectListItem> ProjectOptions { get; set; } = new();
}

public class AttendanceBoardViewModel
{
    public DateTime Date { get; set; }
    public List<AttendanceBoardRow> Rows { get; set; } = new();
    public List<SelectListItem> Projects { get; set; } = new();
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int LateCount { get; set; }
    public int LeaveCount { get; set; }
}

public class AttendanceHistoryViewModel
{
    public DateTime? Date { get; set; }
    public int? ProjectId { get; set; }
    public List<Attendance> Rows { get; set; } = new();
    public List<SelectListItem> Projects { get; set; } = new();
    public List<SelectListItem> Workers { get; set; } = new();
}

public class WorkersViewModel
{
    public bool? Active { get; set; }
    public List<WorkerListRow> Rows { get; set; } = new();
}

public class WorkerListRow
{
    public Worker Worker { get; set; } = new();
    public int ExpiredDocs { get; set; }
    public int ExpiringDocs { get; set; }
}

public class WorkerFormViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime HireDate { get; set; } = DateTime.UtcNow.Date;
    public ShiftType Shift { get; set; } = ShiftType.Day;
    public bool IsActive { get; set; } = true;
    public int? ContractorCompanyId { get; set; }
    public List<ContractorCompany> Contractors { get; set; } = new();
}

public class WorkerDetailsViewModel
{
    public Worker Worker { get; set; } = new();
    public List<OnboardingDocument> Documents { get; set; } = new();
    public List<Attendance> RecentAttendance { get; set; } = new();
}

public class ContractorsViewModel
{
    public bool? Active { get; set; }
    public List<ContractorListRow> Rows { get; set; } = new();
}

public class ContractorListRow
{
    public ContractorCompany Contractor { get; set; } = new();
    public int WorkerCount { get; set; }
}

public class ContractorFormViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Trade { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public static class WorkforceBadges
{
    public static string Attendance(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => "success",
        AttendanceStatus.Late => "warning",
        AttendanceStatus.Absent => "danger",
        _ => "secondary"
    };

    public static string Expiry(DateTime? expiry)
    {
        if (!expiry.HasValue) return "secondary";
        var today = DateTime.UtcNow.Date;
        if (expiry.Value.Date < today) return "danger";
        if (expiry.Value.Date <= today.AddDays(30)) return "warning";
        return "success";
    }
}
