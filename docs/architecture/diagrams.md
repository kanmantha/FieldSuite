# FieldSuite - Architecture Diagrams

All diagrams are Mermaid. They render on GitHub, in VS Code (Markdown Preview Mermaid Support), and in any Mermaid-compatible viewer. Blocks are kept syntactically simple: quoted node labels, single-word ER relationship labels, and no exotic syntax.

| # | Diagram | Type |
|---|---|---|
| 1 | System context | `C4Context` |
| 2 | Container diagram | `C4Container` |
| 3 | Module diagram | `flowchart` |
| 4 | Entity relationship diagram (all entities) | `erDiagram` |
| 5 | Sequence - permit approval flow | `sequenceDiagram` |
| 6 | Sequence - incident report flow | `sequenceDiagram` |
| 7 | Deployment diagram | `flowchart` |

---

## 1. C4 Context - users, FieldSuite, external systems

```mermaid
C4Context
    title FieldSuite system context
    Person(admin, "Admin or business owner", "Manages users, projects, policy and approvals")
    Person(site, "Site manager and safety officer", "Runs site operations, permits, incidents, attendance")
    Person(qc, "QC inspector and estimator", "Inspections, snags, NCRs, estimates and proposals")
    Person(worker, "Field worker", "Reports incidents, confirms toolbox talks, views own records")
    System(fs, "FieldSuite", "Modular monolith B2B SaaS for SME construction and manufacturing field operations")
    System_Ext(db, "PostgreSQL 16", "Primary datastore, all tenant rows carry OrganizationId")
    System_Ext(llm, "LLM provider", "Optional OpenAI compatible endpoint for drafts and chat")
    Rel(admin, fs, "Uses via browser over HTTPS")
    Rel(site, fs, "Uses via browser over HTTPS")
    Rel(qc, fs, "Uses via browser over HTTPS")
    Rel(worker, fs, "Uses via mobile browser over HTTPS")
    Rel(fs, db, "Reads and writes over TCP 5432")
    Rel(fs, llm, "Calls when configured, otherwise falls back to rules")
```

## 2. C4 Container - Web, Infrastructure, Domain, database

```mermaid
C4Container
    title FieldSuite container diagram
    Person(user, "Authenticated user", "One of six roles in exactly one organization")
    System_Boundary(app, "FieldSuite modular monolith") {
        Container(web, "FieldSuite.Web", "ASP.NET Core 10 MVC", "Razor views, Bootstrap 5, controllers, cookie authentication, authorization, DI composition root")
        Container(infra, "FieldSuite.Infrastructure", "Class library", "EF Core DbContext, migrations, seed, heuristic services, LLM client, file storage, background jobs")
        Container(domain, "FieldSuite.Domain", "Class library", "Entities, enums, state machine guards, computed values, service interfaces")
    }
    ContainerDb(db, "PostgreSQL 16", "Relational database", "Tenant data with row level tenancy and audit log")
    ContainerDb(files, "Local file store", "wwwroot uploads", "Photos, permits and document files per organization")
    System_Ext(llm, "LLM provider", "Optional OpenAI compatible endpoint")
    Rel(user, web, "HTTPS, cookie session and antiforgery token")
    Rel(web, domain, "Uses entities, rules and interfaces")
    Rel(web, infra, "Uses DbContext, query and background services")
    Rel(infra, domain, "Implements domain interfaces")
    Rel(infra, db, "EF Core over Npgsql")
    Rel(web, files, "Stores and serves tenant scoped uploads")
    Rel(infra, llm, "HTTPS, optional with graceful fallback")
```

## 3. Module diagram - five modules plus shared kernel

```mermaid
flowchart LR
    subgraph KERNEL["Shared kernel - FieldSuite.Domain"]
        SHARED["Shared entities: Organization, AppUser, Project, AuditLog, Notification"]
        SERVICES["Shared services: ITenantContext, IAuditService, INotificationService, IReferenceNumberAllocator, IFileStorage, IDashboardService"]
    end

    subgraph M1["Module 1 - Safety and EHS"]
        SAFETY["Incident, CorrectiveAction, ToolboxTalk, ToolboxAttendance"]
    end

    subgraph M2["Module 2 - Quality and QC"]
        QUALITY["Inspection, InspectionItem, Snag, NCR"]
    end

    subgraph M3["Module 3 - ePTW"]
        PERMITS["WorkPermit, PermitChecklistItem, PermitApproval"]
    end

    subgraph M4["Module 4 - Estimation"]
        EST["Estimate, EstimateCategory, EstimateItem"]
    end

    subgraph M5["Module 5 - Workforce"]
        WORKFORCE["ContractorCompany, Worker, Attendance, OnboardingDocument"]
    end

    subgraph HOOKS["Heuristics and AI - interfaces in Domain, implementations in Infrastructure"]
        RISK["IPermitRiskScoring"]
        COST["ICostSuggestion"]
        NUDGE["INudgeEngine"]
        LLMCLIENT["ILLMClient"]
    end

    M1 --> KERNEL
    M2 --> KERNEL
    M3 --> KERNEL
    M4 --> KERNEL
    M5 --> KERNEL

    M3 --> RISK
    M4 --> COST
    M1 --> NUDGE
    M2 --> NUDGE
    M5 --> NUDGE
    M5 -->|"attendance vs active permits"| M3
    M2 -->|"snag raised from failed items"| M2
    WEBUI["FieldSuite.Web - controllers, views, dashboard"] --> M1
    WEBUI --> M2
    WEBUI --> M3
    WEBUI --> M4
    WEBUI --> M5
    WEBUI --> LLMCLIENT
    WEBUI --> SERVICES
```

## 4. ERD - all entities and relations

```mermaid
erDiagram
    Organization {
        int Id PK
        string Name
        datetime CreatedAt
    }
    AppUser {
        string Id PK
        int OrganizationId FK
        string FullName
        string Role
        string UserName
        string Email
    }
    Project {
        int Id PK
        int OrganizationId FK
        string Name
        string Code
        string ClientName
        string Address
        date StartDate
        date EndDate
        string Status
        bool IsActive
    }
    AuditLog {
        int Id PK
        int OrganizationId FK
        string UserId FK
        string Action
        string EntityType
        string EntityId
        string Detail
        datetime Timestamp
    }
    Notification {
        int Id PK
        int OrganizationId FK
        string UserId FK
        string Message
        string Link
        bool IsRead
        datetime CreatedAt
    }
    Incident {
        int Id PK
        int OrganizationId FK
        int ProjectId FK
        string Reference
        string Type
        string Severity
        string Title
        string Description
        datetime OccurredAt
        string Location
        string RootCause
        string ReportedById FK
        string Status
        string ReviewedById FK
    }
    CorrectiveAction {
        int Id PK
        int IncidentId FK
        string Description
        string AssignedToId FK
        date DueDate
        string Status
    }
    ToolboxTalk {
        int Id PK
        int ProjectId FK
        string Topic
        date TalkDate
        int DurationMinutes
        string FacilitatorId FK
        string Notes
    }
    ToolboxAttendance {
        int Id PK
        int ToolboxTalkId FK
        int WorkerId FK
    }
    Inspection {
        int Id PK
        int ProjectId FK
        string Reference
        string Type
        string Title
        date InspectionDate
        string InspectorId FK
        string Status
        string OverallNotes
    }
    InspectionItem {
        int Id PK
        int InspectionId FK
        string ChecklistText
        string Result
        string Comment
    }
    Snag {
        int Id PK
        int ProjectId FK
        string Reference
        string Title
        string Description
        string Location
        string Priority
        string RaisedById FK
        string AssignedToId FK
        date DueDate
        string PhotoPath
        string Status
        datetime ClosedAt
    }
    NCR {
        int Id PK
        int ProjectId FK
        string Reference
        string Title
        string Severity
        string Description
        date DetectedDate
        string ContainmentAction
        string Status
    }
    WorkPermit {
        int Id PK
        int OrganizationId FK
        int ProjectId FK
        string Reference
        string Type
        string Title
        string Description
        string Location
        string RequestedById FK
        datetime RequestedStart
        datetime RequestedEnd
        string RiskLevel
        string Status
        bool IsolationVerified
        bool GasTestRequired
        string SpecialConditions
        datetime ActualStart
        datetime ActualEnd
    }
    PermitChecklistItem {
        int Id PK
        int WorkPermitId FK
        string Text
        bool IsChecked
        string CheckedById FK
        datetime CheckedAt
    }
    PermitApproval {
        int Id PK
        int WorkPermitId FK
        int Level
        string ApproverRole
        string ApproverId FK
        string Decision
        string Comment
        datetime DecidedAt
    }
    Estimate {
        int Id PK
        int OrganizationId FK
        int ProjectId FK
        string Reference
        string Title
        string ClientName
        string Status
        date ValidUntil
        string Notes
        string CreatedById FK
        datetime CreatedAt
        decimal MarginPercent
    }
    EstimateCategory {
        int Id PK
        int EstimateId FK
        string Name
        int SortOrder
    }
    EstimateItem {
        int Id PK
        int EstimateCategoryId FK
        string Description
        decimal Quantity
        string Unit
        decimal UnitRate
        decimal WastePercent
    }
    ContractorCompany {
        int Id PK
        int OrganizationId FK
        string Name
        string ContactPerson
        string Phone
        string Email
        string Trade
        bool IsActive
    }
    Worker {
        int Id PK
        int OrganizationId FK
        int ContractorCompanyId FK
        string FullName
        string JobTitle
        string Phone
        string Email
        date HireDate
        string Shift
        bool IsActive
    }
    Attendance {
        int Id PK
        int WorkerId FK
        int ProjectId FK
        date WorkDate
        datetime ClockIn
        datetime ClockOut
        decimal HoursWorked
        string Status
        string Notes
    }
    OnboardingDocument {
        int Id PK
        int WorkerId FK
        string Type
        string DocumentNumber
        date IssuedDate
        date ExpiryDate
        string FilePath
    }

    Organization ||--o{ AppUser : has
    Organization ||--o{ Project : owns
    Organization ||--o{ AuditLog : holds
    Organization ||--o{ Notification : queues
    Organization ||--o{ WorkPermit : issues
    Organization ||--o{ Estimate : sponsors
    Organization ||--o{ ContractorCompany : engages
    Organization ||--o{ Worker : employs
    AppUser ||--o{ AuditLog : writes
    AppUser ||--o{ Notification : receives
    AppUser ||--o{ Incident : reports
    AppUser ||--o{ Incident : reviews
    AppUser ||--o{ CorrectiveAction : assigned
    AppUser ||--o{ ToolboxTalk : facilitates
    AppUser ||--o{ Inspection : inspects
    AppUser ||--o{ Snag : raises
    AppUser ||--o{ Snag : resolves
    AppUser ||--o{ WorkPermit : requests
    AppUser ||--o{ PermitChecklistItem : verifies
    AppUser ||--o{ PermitApproval : decides
    AppUser ||--o{ Estimate : creates
    Project ||--o{ Incident : contains
    Project ||--o{ ToolboxTalk : hosts
    Project ||--o{ Inspection : schedules
    Project ||--o{ Snag : generates
    Project ||--o{ NCR : flags
    Project ||--o{ WorkPermit : authorizes
    Project |o--o{ Estimate : scopes
    Project ||--o{ Attendance : gathers
    Incident ||--o{ CorrectiveAction : tracks
    ToolboxTalk ||--o{ ToolboxAttendance : records
    Worker ||--o{ ToolboxAttendance : attends
    Inspection ||--o{ InspectionItem : lists
    WorkPermit ||--o{ PermitChecklistItem : holds
    WorkPermit ||--o{ PermitApproval : requires
    Estimate ||--o{ EstimateCategory : groups
    EstimateCategory ||--o{ EstimateItem : comprises
    ContractorCompany |o--o{ Worker : hires
    Worker ||--o{ Attendance : logs
    Worker ||--o{ OnboardingDocument : carries
```

Notes:

- Every child row carries its own `OrganizationId` (directly or through a parent) for row-level tenancy; the diagram shows it explicitly where the specification lists it.
- `ToolboxAttendance.WorkerId` and `Attendance.WorkerId` reference the `Worker` entity (Module 5); all other `...ById` fields reference `AppUser`.
- `Estimate.ProjectId` and `Worker.ContractorCompanyId` are optional (zero-or-one on the parent side).

## 5. Sequence - permit approval flow

```mermaid
sequenceDiagram
    autonumber
    actor SM as Site Manager
    actor SO as Safety Officer
    actor ADM as Admin
    participant CTRL as WorkPermitController
    participant SVC as PermitService
    participant RISK as IPermitRiskScoring
    participant AUD as IAuditService
    participant NOT as INotificationService
    participant DB as PostgreSQL

    SM->>CTRL: POST permits create and submit
    CTRL->>SVC: CreateAndSubmitAsync(dto, user)
    SVC->>RISK: Score(type, isolation, durationHours, crewSize)
    RISK-->>SVC: RiskLevel High, approval levels 3
    SVC->>DB: INSERT WorkPermit PWT-0001 status PendingApproval
    SVC->>DB: INSERT PermitApproval rows Level 1 to 3, all Pending
    SVC->>AUD: Write create and submit entries
    SVC->>NOT: Notify SiteManager role, level 1 decision pending
    NOT-->>SM: In-app notification
    SM->>CTRL: POST permits 1 decisions level 1 Approved
    CTRL->>SVC: DecideAsync(permitId, level 1, Approved, user)
    SVC->>DB: UPDATE PermitApproval level 1 Approved
    SVC->>NOT: Notify SafetyOfficer role, level 2 decision pending
    NOT-->>SO: In-app notification
    SO->>CTRL: POST permits 1 decisions level 2 Approved
    CTRL->>SVC: DecideAsync(permitId, level 2, Approved, user)
    SVC->>DB: UPDATE PermitApproval level 2 Approved
    SVC->>NOT: Notify Admin role, level 3 decision pending
    NOT-->>ADM: In-app notification
    ADM->>CTRL: POST permits 1 decisions level 3 Approved
    CTRL->>SVC: DecideAsync(permitId, level 3, Approved, user)
    SVC->>DB: UPDATE PermitApproval level 3 Approved
    SVC->>DB: UPDATE WorkPermit status Approved
    SVC->>AUD: Write approve entry, chain complete
    SVC->>NOT: Notify requester, permit approved
    SM->>CTRL: POST permits 1 activate
    CTRL->>SVC: ActivateAsync(permitId, user)
    alt all checklist checked and isolation and gas test satisfied
        SVC->>DB: UPDATE WorkPermit status Active, ActualStart set
        SVC->>AUD: Write activate entry
        SVC->>NOT: Notify requester and SafetyOfficer, permit active
    else checklist or isolation incomplete
        SVC-->>SM: 400 permit activate blocked, status unchanged
    end
    Note over SVC,DB: Hourly job later flips any permit past RequestedEnd to Expired
```

## 6. Sequence - incident report flow

```mermaid
sequenceDiagram
    autonumber
    actor REP as Reporter (Worker or Site Manager)
    actor SO as Safety Officer
    participant CTRL as IncidentController
    participant SVC as IncidentService
    participant REF as IReferenceNumberAllocator
    participant AUD as IAuditService
    participant NOT as INotificationService
    participant NUDGE as INudgeEngine
    participant DB as PostgreSQL

    REP->>CTRL: POST safety incidents create
    CTRL->>SVC: CreateAsync(dto, user)
    SVC->>REF: NextAsync(orgId, SIN)
    REF-->>SVC: SIN-0001
    SVC->>DB: INSERT Incident status Draft, OrganizationId stamped
    SVC->>AUD: Write create entry
    SVC-->>REP: 201 Created, reference SIN-0001
    REP->>CTRL: POST safety incidents 1 submit
    CTRL->>SVC: TransitionAsync(id, Reported, user)
    SVC->>DB: UPDATE Incident status Reported
    SVC->>AUD: Write transition entry
    SVC->>NOT: Notify SafetyOfficer, new incident reported
    NOT-->>SO: In-app notification
    SO->>CTRL: POST safety incidents 1 transition Investigating
    CTRL->>SVC: TransitionAsync(id, Investigating, safetyOfficer)
    SVC->>DB: UPDATE Incident status Investigating
    SO->>CTRL: POST safety incidents 1 corrective actions
    CTRL->>SVC: AddCorrectiveActionAsync(incidentId, dto)
    SVC->>DB: INSERT CorrectiveAction status Open, DueDate set
    SVC->>NOT: Notify assignee, corrective action assigned
    SO->>CTRL: POST safety incidents 1 transition Resolved
    CTRL->>SVC: TransitionAsync(id, Resolved, safetyOfficer)
    SVC->>DB: UPDATE Incident status Resolved, ReviewedById set
    SVC->>AUD: Write transition entry with root cause
    SVC->>NOT: Notify reporter, incident resolved
    NUDGE->>DB: SELECT corrective actions past DueDate and not Done
    NUDGE->>NOT: Notify assignee, corrective action overdue
    SO->>CTRL: POST safety incidents 1 transition Closed
    CTRL->>SVC: TransitionAsync(id, Closed, safetyOfficer)
    SVC->>DB: UPDATE Incident status Closed
    SVC->>AUD: Write close entry
    SVC->>NOT: Notify reporter, incident closed
```

## 7. Deployment diagram

```mermaid
flowchart TB
    subgraph INTERNET["Public network"]
        BROWSER["Browser or mobile browser"]
    end

    subgraph HOST["Single application host"]
        subgraph PROXY["Reverse proxy - Caddy or Nginx"]
            TLS["TLS termination and static file serving"]
        end
        subgraph KESTREL["Kestrel - ASP.NET Core 10 runtime"]
            WEB["FieldSuite.Web MVC application"]
            JOBS["BackgroundService hourly sweep"]
            HOOKS["Heuristics and ILLMClient"]
        end
        subgraph PG["PostgreSQL 16 service"]
            DB[("fieldsuite database - OrganizationId row level tenancy")]
        end
        UPLOADS[("wwwroot uploads - tenant scoped photos and documents")]
        WAL["WAL archive and nightly pg_dump"]
    end

    LLM["Optional OpenAI compatible LLM provider"]
    BACKUP["Off-host encrypted backup storage"]

    BROWSER -->|"HTTPS"| TLS
    TLS --> WEB
    WEB -->|"EF Core and Npgsql over TCP 5432"| DB
    JOBS -->|"permit expiry, estimate expiry, nudge sweep"| DB
    WEB -->|"file upload and authorized download"| UPLOADS
    HOOKS -->|"HTTPS only when configured"| LLM
    DB --> WAL
    WAL -->|"14 day WAL, 30 day daily backups"| BACKUP
```

## 8. Rendering checklist

- Each block is fenced as `mermaid`.
- C4 blocks use `C4Context` / `C4Container` with `title`, `Person`, `System`, `System_Boundary`, `Container`, `ContainerDb`, `System_Ext`, `Rel`.
- ERD relationship labels are single lowercase words; optional parents use `|o--o{`.
- Sequence diagrams declare all participants up front and close every `alt` with `end`.
- Flowchart subgraphs are quoted (`subgraph ID["Label"]`) and every `subgraph` has a matching `end`.
