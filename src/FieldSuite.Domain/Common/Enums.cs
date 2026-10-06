namespace FieldSuite.Domain.Common;

public enum ProjectStatus { Active = 0, OnHold = 1, Completed = 2 }

public enum IncidentType { Incident = 0, NearMiss = 1, FirstAid = 2 }
public enum Severity { Low = 0, Medium = 1, High = 2, Critical = 3 }
public enum IncidentStatus { Draft = 0, Reported = 1, Investigating = 2, Resolved = 3, Closed = 4 }
public enum CorrectiveActionStatus { Open = 0, InProgress = 1, Done = 2, Overdue = 3 }

public enum InspectionType { Internal = 0, Client = 1, ThirdParty = 2 }
public enum InspectionStatus { Draft = 0, InProgress = 1, Passed = 2, Failed = 3 }
public enum CheckResult { Pass = 0, Fail = 1, N_A = 2 }
public enum SnagPriority { Low = 0, Medium = 1, High = 2, Critical = 3 }
public enum SnagStatus { Open = 0, Assigned = 1, InProgress = 2, Retest = 3, Closed = 4, Rejected = 5 }
public enum NCRStatus { Open = 0, Containment = 1, Investigation = 2, Closed = 3 }

public enum PermitType { HotWork = 0, ConfinedSpace = 1, Height = 2, Electrical = 3, Excavation = 4, Lifting = 5 }
public enum RiskLevel { Low = 0, Medium = 1, High = 2, Extreme = 3 }
public enum PermitStatus { Draft = 0, PendingApproval = 1, Approved = 2, Active = 3, Suspended = 4, Closed = 5, Rejected = 6, Expired = 7 }
public enum ApprovalDecision { Pending = 0, Approved = 1, Rejected = 2 }

public enum EstimateStatus { Draft = 0, Sent = 1, Accepted = 2, Rejected = 3, Expired = 4 }

public enum ShiftType { Morning = 0, Day = 1, Night = 2 }
public enum AttendanceStatus { Present = 0, Absent = 1, Late = 2, OnLeave = 3 }
public enum WorkerDocumentType { ID = 0, Contract = 1, SafetyCert = 2, Medical = 3, Insurance = 4 }
