using FieldSuite.Domain.Common;

namespace FieldSuite.Domain.Entities;

public class ContractorCompany : Entity
{
    public int OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Trade { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Worker> Workers { get; set; } = new List<Worker>();
}

public class Worker : Entity
{
    public int OrganizationId { get; set; }
    public int? ContractorCompanyId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime HireDate { get; set; } = DateTime.UtcNow.Date;
    public ShiftType Shift { get; set; } = ShiftType.Day;
    public bool IsActive { get; set; } = true;

    public ContractorCompany? ContractorCompany { get; set; }
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<OnboardingDocument> OnboardingDocuments { get; set; } = new List<OnboardingDocument>();
}

public class Attendance : Entity
{
    public int WorkerId { get; set; }
    public int ProjectId { get; set; }
    public DateTime WorkDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }
    public decimal? HoursWorked { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string Notes { get; set; } = string.Empty;

    public Worker? Worker { get; set; }
    public Project? Project { get; set; }
}

public class OnboardingDocument : Entity
{
    public int WorkerId { get; set; }
    public WorkerDocumentType Type { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime? IssuedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string FilePath { get; set; } = string.Empty;

    public Worker? Worker { get; set; }
}
