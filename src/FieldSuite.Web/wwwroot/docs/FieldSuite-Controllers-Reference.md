# FieldSuite Controllers — Method Reference (with functionality)


## AccountControllerController

### Login (line 19)
`csharp
public IActionResult Login(string? returnUrl = null)
`
- Renders empty/default view.
- Renders view (possibly with model).
- Redirects to another action.

### Login (line 29)
`csharp
public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
`
- Renders empty/default view.
- Renders view (possibly with model).
- Redirects to another action.
- Redirects to URL.
- Identity/auth operations.
- Input validation.

### Logout (line 60)
`csharp
public async Task<IActionResult> Logout()
`
- Redirects to another action.
- Identity/auth operations.

## AppControllerController

## AssistantControllerController

### Index (line 20)
`csharp
public IActionResult Index() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask(string question)
`
- Returns JSON (AJAX).

### Ask (line 25)
`csharp
public async Task<IActionResult> Ask(string question)
`
- Returns JSON (AJAX).

### Draft (line 33)
`csharp
public async Task<IActionResult> Draft(string kind, string rawInput)
`
- Returns JSON (AJAX).

## DashboardControllerController

### Index (line 21)
`csharp
public async Task<IActionResult> Index()
`
- Renders empty/default view.
- Renders view (possibly with model).
- Queries/updates database.

## DocumentsControllerController

### Index (line 25)
`csharp
public IActionResult Index()
`
- Renders view (possibly with model).

## EstimationControllerController

### Index (line 24)
`csharp
public async Task<IActionResult> Index(EstimateStatus? status = null)
`
- Renders view (possibly with model).
- Queries/updates database.

### Create (line 55)
`csharp
public async Task<IActionResult> Create()
`
- Renders view (possibly with model).
- Queries/updates database.

### Create (line 67)
`csharp
public async Task<IActionResult> Create(EstimateCreateViewModel model)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.
- Input validation.

### Details (line 115)
`csharp
public async Task<IActionResult> Details(int id)
`
- Renders view (possibly with model).
- Queries/updates database.

### ItemAdd (line 134)
`csharp
public async Task<IActionResult> ItemAdd(int categoryId, string description, decimal quantity, string unit, decimal unitRate, decimal wastePercent)
`
- Redirects to another action.
- Queries/updates database.

### ItemEdit (line 163)
`csharp
public async Task<IActionResult> ItemEdit(int id, string description, decimal quantity, string unit, decimal unitRate, decimal wastePercent)
`
- Redirects to another action.
- Queries/updates database.

### ItemDelete (line 189)
`csharp
public async Task<IActionResult> ItemDelete(int id)
`
- Redirects to another action.
- Queries/updates database.

### CategoryAdd (line 208)
`csharp
public async Task<IActionResult> CategoryAdd(int estimateId, string name)
`
- Redirects to another action.
- Queries/updates database.

### SuggestRate (line 233)
`csharp
public async Task<IActionResult> SuggestRate(string unit, string description)
`
- Returns JSON (AJAX).

### StatusChange (line 242)
`csharp
public async Task<IActionResult> StatusChange(int id, EstimateStatus to)
`
- Redirects to another action.
- Queries/updates database.

### Proposal (line 270)
`csharp
public async Task<IActionResult> Proposal(int id)
`
- Renders view (possibly with model).
- Queries/updates database.

## HomeControllerController

### Error (line 6)
`csharp
public IActionResult Error() =>
        View(new ErrorViewModel
`
- Queries/updates database.

## PermitsControllerController

### Index (line 83)
`csharp
public async Task<IActionResult> Index(PermitStatus? status, PermitType? type)
`
- Renders view (possibly with model).
- Queries/updates database.

### Create (line 107)
`csharp
public async Task<IActionResult> Create()
`
- Renders view (possibly with model).

### Create (line 119)
`csharp
public async Task<IActionResult> Create(PermitFormViewModel model)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.

### Details (line 177)
`csharp
public async Task<IActionResult> Details(int id)
`
- Renders view (possibly with model).

### Edit (line 201)
`csharp
public async Task<IActionResult> Edit(int id)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.

### Edit (line 231)
`csharp
public async Task<IActionResult> Edit(int id, PermitFormViewModel model)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.

### ChecklistToggle (line 291)
`csharp
public async Task<IActionResult> ChecklistToggle(int id, int itemId)
`
- Redirects to another action.
- Queries/updates database.

### Approve (line 317)
`csharp
public async Task<IActionResult> Approve(int id, int approvalId, string? comment)
`
- Redirects to another action.
- Queries/updates database.
- Sends notifications.

### Reject (line 387)
`csharp
public async Task<IActionResult> Reject(int id, int approvalId, string? comment)
`
- Redirects to another action.
- Queries/updates database.
- Sends notifications.

### Submit (line 435)
`csharp
public async Task<IActionResult> Submit(int id)
`
- Redirects to another action.
- Queries/updates database.
- Sends notifications.

### Start (line 468)
`csharp
public async Task<IActionResult> Start(int id)
`
- Redirects to another action.
- Queries/updates database.

### Suspend (line 496)
`csharp
public async Task<IActionResult> Suspend(int id)
`
- Redirects to another action.
- Queries/updates database.

### Resume (line 517)
`csharp
public async Task<IActionResult> Resume(int id)
`
- Redirects to another action.
- Queries/updates database.

### Close (line 538)
`csharp
public async Task<IActionResult> Close(int id)
`
- Redirects to another action.
- Queries/updates database.

## ProjectsControllerController

### Index (line 14)
`csharp
public async Task<IActionResult> Index()
`
- Renders view (possibly with model).
- Queries/updates database.

### Create (line 37)
`csharp
public IActionResult Create() => View(new Project
`
- Controller action.

### Create (line 41)
`csharp
public async Task<IActionResult> Create(Project model)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.
- Input validation.

### Details (line 59)
`csharp
public async Task<IActionResult> Details(int id)
`
- Renders view (possibly with model).
- Queries/updates database.

### Edit (line 73)
`csharp
public async Task<IActionResult> Edit(int id)
`
- Renders view (possibly with model).
- Queries/updates database.

### Edit (line 82)
`csharp
public async Task<IActionResult> Edit(int id, Project model)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.
- Input validation.

## QualityControllerController

### Index (line 30)
`csharp
public async Task<IActionResult> Index(SnagStatus? status, int? projectId)
`
- Renders view (possibly with model).
- Queries/updates database.

### SnagCreate (line 54)
`csharp
public async Task<IActionResult> SnagCreate()
`
- Renders view (possibly with model).

### SnagCreate (line 64)
`csharp
public async Task<IActionResult> SnagCreate(SnagCreateVm vm)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.
- Sends notifications.

### SnagDetails (line 120)
`csharp
public async Task<IActionResult> SnagDetails(int id)
`
- Renders view (possibly with model).
- Queries/updates database.

### SnagEdit (line 133)
`csharp
public async Task<IActionResult> SnagEdit(int id)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.

### SnagEdit (line 154)
`csharp
public async Task<IActionResult> SnagEdit(SnagEditVm vm)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.

### SnagTransition (line 181)
`csharp
public async Task<IActionResult> SnagTransition(int id, SnagStatus to)
`
- Redirects to another action.
- Queries/updates database.

### Inspections (line 210)
`csharp
public async Task<IActionResult> Inspections()
`
- Renders view (possibly with model).
- Queries/updates database.

### InspectionCreate (line 222)
`csharp
public async Task<IActionResult> InspectionCreate()
`
- Renders view (possibly with model).

### InspectionCreate (line 231)
`csharp
public async Task<IActionResult> InspectionCreate(InspectionCreateVm vm)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.

### InspectionDetails (line 257)
`csharp
public async Task<IActionResult> InspectionDetails(int id)
`
- Renders view (possibly with model).
- Queries/updates database.

### InspectionItemAdd (line 272)
`csharp
public async Task<IActionResult> InspectionItemAdd(int inspectionId, string checklistText)
`
- Redirects to another action.
- Queries/updates database.

### InspectionItemResult (line 292)
`csharp
public async Task<IActionResult> InspectionItemResult(int itemId, CheckResult result, string? comment)
`
- Redirects to another action.
- Queries/updates database.

### InspectionFinalize (line 306)
`csharp
public async Task<IActionResult> InspectionFinalize(int id)
`
- Redirects to another action.
- Queries/updates database.
- Sends notifications.

### NCRs (line 329)
`csharp
public async Task<IActionResult> NCRs()
`
- Renders view (possibly with model).
- Queries/updates database.

### NCRCreate (line 341)
`csharp
public async Task<IActionResult> NCRCreate()
`
- Renders view (possibly with model).

### NCRCreate (line 350)
`csharp
public async Task<IActionResult> NCRCreate(NcrCreateVm vm)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.
- Sends notifications.

### NCRDetails (line 378)
`csharp
public async Task<IActionResult> NCRDetails(int id)
`
- Renders view (possibly with model).
- Queries/updates database.

### NCRTransition (line 393)
`csharp
public async Task<IActionResult> NCRTransition(int id, NCRStatus to)
`
- Redirects to another action.
- Queries/updates database.

## SafetyControllerController

### Index (line 31)
`csharp
public async Task<IActionResult> Index(IncidentStatus? status, int? projectId)
`
- Renders view (possibly with model).
- Queries/updates database.

### IncidentCreate (line 62)
`csharp
public async Task<IActionResult> IncidentCreate()
`
- Renders view (possibly with model).

### IncidentCreate (line 70)
`csharp
public async Task<IActionResult> IncidentCreate(IncidentCreateVm model, string? saveAsDraft)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.
- Sends notifications.
- Input validation.

### IncidentDetails (line 128)
`csharp
public async Task<IActionResult> IncidentDetails(int id)
`
- Renders view (possibly with model).
- Queries/updates database.

### IncidentEdit (line 149)
`csharp
public async Task<IActionResult> IncidentEdit(int id)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.

### IncidentEdit (line 177)
`csharp
public async Task<IActionResult> IncidentEdit(int id, IncidentEditVm model)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.
- Input validation.

### IncidentTransition (line 223)
`csharp
public async Task<IActionResult> IncidentTransition(int id, IncidentStatus to)
`
- Redirects to another action.
- Queries/updates database.
- Sends notifications.

### CorrectiveActionAdd (line 258)
`csharp
public async Task<IActionResult> CorrectiveActionAdd(int incidentId, string description, string? assignedToId, DateTime dueDate)
`
- Redirects to another action.
- Queries/updates database.
- Sends notifications.

### CorrectiveActionUpdate (line 311)
`csharp
public async Task<IActionResult> CorrectiveActionUpdate(int id, CorrectiveActionStatus status)
`
- Redirects to another action.
- Queries/updates database.

### ToolboxTalks (line 330)
`csharp
public async Task<IActionResult> ToolboxTalks(int? projectId)
`
- Renders view (possibly with model).
- Queries/updates database.

### ToolboxTalkCreate (line 355)
`csharp
public async Task<IActionResult> ToolboxTalkCreate()
`
- Renders view (possibly with model).

### ToolboxTalkCreate (line 367)
`csharp
public async Task<IActionResult> ToolboxTalkCreate(ToolboxTalkCreateVm model)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.
- Input validation.

### ToolboxTalkDetails (line 422)
`csharp
public async Task<IActionResult> ToolboxTalkDetails(int id)
`
- Renders view (possibly with model).
- Queries/updates database.

## WorkforceControllerController

### Index (line 19)
`csharp
public async Task<IActionResult> Index()
`
- Renders view (possibly with model).
- Queries/updates database.

### AttendanceMark (line 70)
`csharp
public async Task<IActionResult> AttendanceMark(int workerId, AttendanceStatus status, int projectId)
`
- Redirects to another action.
- Queries/updates database.

### AttendanceClockOut (line 128)
`csharp
public async Task<IActionResult> AttendanceClockOut(int workerId)
`
- Redirects to another action.
- Queries/updates database.

### Attendance (line 156)
`csharp
public async Task<IActionResult> Attendance(DateTime? date, int? projectId)
`
- Renders view (possibly with model).
- Queries/updates database.

### AttendanceCreate (line 194)
`csharp
public async Task<IActionResult> AttendanceCreate(int workerId, int projectId, DateTime? workDate,
        AttendanceStatus status, string? clockIn, string? clockOut)
`
- Redirects to another action.
- Queries/updates database.

### Workers (line 274)
`csharp
public async Task<IActionResult> Workers(bool? active = true)
`
- Renders view (possibly with model).
- Queries/updates database.

### WorkerCreate (line 312)
`csharp
public async Task<IActionResult> WorkerCreate()
`
- Renders view (possibly with model).

### WorkerCreate (line 324)
`csharp
public async Task<IActionResult> WorkerCreate(WorkerFormViewModel model)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.
- Input validation.

### WorkerDetails (line 359)
`csharp
public async Task<IActionResult> WorkerDetails(int id)
`
- Renders view (possibly with model).
- Queries/updates database.

### WorkerEdit (line 391)
`csharp
public async Task<IActionResult> WorkerEdit(int id)
`
- Renders view (possibly with model).
- Queries/updates database.

### WorkerEdit (line 415)
`csharp
public async Task<IActionResult> WorkerEdit(int id, WorkerFormViewModel model)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.
- Input validation.

### DocumentAdd (line 456)
`csharp
public async Task<IActionResult> DocumentAdd(int workerId, WorkerDocumentType type, string documentNumber,
        DateTime? issuedDate, DateTime? expiryDate, string? filePath)
`
- Redirects to another action.
- Queries/updates database.

### DocumentDelete (line 490)
`csharp
public async Task<IActionResult> DocumentDelete(int id)
`
- Redirects to another action.
- Queries/updates database.

### Contractors (line 508)
`csharp
public async Task<IActionResult> Contractors(bool? active)
`
- Renders view (possibly with model).
- Queries/updates database.

### ContractorCreate (line 534)
`csharp
public IActionResult ContractorCreate() => View(new ContractorFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SiteManager,Admin")]
    public async Task<IActionResult> ContractorCreate(ContractorFormViewModel model)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.
- Input validation.

### ContractorCreate (line 539)
`csharp
public async Task<IActionResult> ContractorCreate(ContractorFormViewModel model)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.
- Input validation.

### ContractorEdit (line 567)
`csharp
public async Task<IActionResult> ContractorEdit(int id)
`
- Renders view (possibly with model).
- Queries/updates database.

### ContractorEdit (line 588)
`csharp
public async Task<IActionResult> ContractorEdit(int id, ContractorFormViewModel model)
`
- Renders view (possibly with model).
- Redirects to another action.
- Queries/updates database.
- Input validation.
