# SRS - Module 1: Safety & EHS

| Field | Value |
|---|---|
| Document ID | FS-SRS-SAF |
| Version | 1.0 |
| Date | 2026-10-06 |
| Scope | Incident register, corrective actions, toolbox talks and attendance |
| Parent document | PRD (FS-PRD-001), section 5 (M1) |

---

## 1. Purpose

This specification defines the functional and data requirements for the Safety & EHS module: a per-organization incident and near-miss register with an enforced workflow, corrective actions tracked to closure, and toolbox talks with verifiable worker attendance. The module produces the compliance evidence described in PRD sections 2 and 4.3.

## 2. Actors and roles

| Role | Module capability summary |
|---|---|
| Admin | Full control: create/edit/transition any record in any status, discard drafts, assign reviewers, view all projects. |
| SiteManager | Create/edit incidents and corrective actions for assigned projects; transition incidents through `Reported`/`Investigating`; manage toolbox talks; cannot close incidents (except `Resolved` to `Closed` is reserved for SafetyOfficer/Admin). |
| SafetyOfficer | Primary actor: reviews incidents (`ReviewedById`), drives state transitions to `Resolved`/`Closed`, creates and reassigns corrective actions, runs toolbox talks. |
| QCInspector | Read-only on incidents, corrective actions, toolbox talks; may raise a corrective action arising from an inspection failure (assigned to SafetyOfficer). |
| Estimator | Read-only on incident and toolbox talk lists (no corrective action editing). |
| Worker | Create incidents (report hazards/near misses) in `Draft`; confirm own toolbox attendance; view own assigned corrective actions. |

## 3. Functional requirements

### 3.1 Incident register

| ID | Requirement |
|---|---|
| FR-SAF-01 | **List incidents.** The system shall list incidents for the caller's organization with server-side pagination (default 50 rows) and filters for Project, Type (`Incident`/`NearMiss`/`FirstAid`), Severity, Status, Occurred-at date range, and free-text search on Title/Reference. Results are always scoped to the caller's `OrganizationId`. |
| FR-SAF-02 | **Create incident.** An authorized user shall be able to create an incident with Project, Type, Severity, Title, Description, OccurredAt, Location; `ReportedById` is set to the current user automatically and is not editable; new records start in status `Draft`. |
| FR-SAF-03 | **Automatic reference.** On first save the system shall assign `Reference` in the format `SIN-NNNN`, sequential per organization starting at `SIN-0001`, allocated inside the insert transaction and immutable for the life of the record. |
| FR-SAF-04 | **Edit incident.** Users with edit permission may update incident fields while status is `Draft`, `Reported`, or `Investigating`. `Reference` and `ReportedById` are never editable. Editing a `Resolved`/`Closed` incident requires Admin and is limited to Description, RootCause, and Location; each such edit writes an audit entry. |
| FR-SAF-05 | **State machine.** The system shall enforce the transitions in section 5.1 only; any other transition attempt returns a validation error (`409`-class outcome) and writes no data. `Closed` is terminal; reopening a `Closed` incident requires Admin and records an audit entry with reason. |
| FR-SAF-06 | **Review stamp.** Transitioning to `Resolved` or `Closed` requires the caller to be SafetyOfficer or Admin; the system shall set `ReviewedById` to the caller at that moment. |
| FR-SAF-07 | **Validation rules.** The system shall enforce the rules in section 6.1 before any create or update persists; all violations are returned together as field-level messages. |
| FR-SAF-08 | **Delete/discard.** There is no delete for records past `Draft`. A `Draft` incident may be discarded by its reporter or an Admin; the discard writes an `AuditLog` entry (action `Discard`) before the row is removed. |

### 3.2 Corrective actions

| ID | Requirement |
|---|---|
| FR-SAF-09 | **Create corrective action.** SafetyOfficer, SiteManager, and Admin shall be able to create one or more corrective actions on an incident with Description, AssignedToId (must be an active user in the same organization), and DueDate; new actions start `Open`. |
| FR-SAF-10 | **State machine (corrective action).** Allowed transitions: `Open -> InProgress -> Done`, `Open -> Overdue`, `InProgress -> Overdue`, `Overdue -> InProgress`, `Overdue -> Done`. `Done` is terminal. Only the assignee, SafetyOfficer, or Admin may transition a status. |
| FR-SAF-11 | **Overdue determination.** A corrective action whose `DueDate` is before today and whose status is not `Done` shall be surfaced as overdue: the system shall present the computed state `Overdue` (persisted when the nudge engine runs) and include the record in the dashboard KPI and in `INudgeEngine` output. |
| FR-SAF-12 | **Reassignment.** SafetyOfficer or Admin may reassign a corrective action; reassignment notifies the new assignee and writes an audit entry containing old/new assignee. |

### 3.3 Toolbox talks and attendance

| ID | Requirement |
|---|---|
| FR-SAF-13 | **Create toolbox talk.** SiteManager, SafetyOfficer, and Admin shall be able to create a toolbox talk with Project, Topic, TalkDate, DurationMinutes, FacilitatorId, Notes. |
| FR-SAF-14 | **Edit toolbox talk.** A talk may be edited by its facilitator, SiteManager, or Admin while `TalkDate` is today or in the future; past talks may be edited only by Admin and only for Notes and DurationMinutes. |
| FR-SAF-15 | **Record attendance.** Authorized users shall be able to record attendance for a talk by selecting multiple active workers in a single save; the system shall create one `ToolboxAttendance` row per worker and reject duplicates for the same `(ToolboxTalkId, WorkerId)`. |
| FR-SAF-16 | **Attendance visibility.** The talk detail view shall show the full attendance roster (worker name, contractor, job title) and the attendance count versus the project's active worker count; attendance rows cannot be individually edited, only added or removed before the talk is locked (Admin after `TalkDate` + 1 day). |

### 3.4 Cross-cutting

| ID | Requirement |
|---|---|
| FR-SAF-17 | **Notifications.** The system shall create in-app notifications for: corrective action assigned, corrective action overdue (nudge), incident transitioned to `Resolved`/`Closed` (to reporter), and toolbox talk scheduled for the facilitator. |
| FR-SAF-18 | **Audit log.** Create, update, and every state transition of Incident and CorrectiveAction shall write an `AuditLog` row (Action, EntityType, EntityId, Detail with field-level summary). |
| FR-SAF-19 | **Dashboard KPIs.** The module shall feed the dashboard tiles "Open incidents" (status not `Closed` and not `Draft`) and "Overdue corrective actions" (computed per FR-SAF-11), scoped to the caller's organization. |
| FR-SAF-20 | **Permissions enforcement.** All rules in section 2 shall be enforced server-side (controller/service), not only by UI hiding; unauthorized attempts return a standard authorization error and write a `Denied` audit entry. |

## 4. Data dictionary

### 4.1 Incident

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| OrganizationId | int (FK -> Organization) | auto | Stamped from caller context; never from client payload; indexed. |
| ProjectId | int (FK -> Project) | yes | Must belong to caller's organization; project must be `IsActive`. |
| Reference | string(16) | auto | `SIN-` + 4-digit sequence per organization; unique with `OrganizationId`; immutable. |
| Type | enum | yes | `Incident`, `NearMiss`, `FirstAid`. |
| Severity | enum | yes | `Low`, `Medium`, `High`, `Critical`. |
| Title | string(200) | yes | Trimmed, non-empty, max 200 characters. |
| Description | string(2000) | no | Max 2000 characters; required when `Severity` is `High` or `Critical`. |
| OccurredAt | datetime | yes | Not later than server current time (5-minute tolerance); not earlier than organization creation. |
| Location | string(300) | yes | Site/area description, max 300 characters. |
| RootCause | string(2000) | no | Required before transition to `Resolved` or `Closed` when `Severity` is `High` or `Critical`. |
| ReportedById | string (FK -> AppUser) | auto | Current user at creation; immutable; must be in same organization. |
| Status | enum | auto | `Draft`, `Reported`, `Investigating`, `Resolved`, `Closed`; default `Draft`. |
| ReviewedById | string? (FK -> AppUser) | no | Set only on transition to `Resolved`/`Closed`; must be SafetyOfficer or Admin. |

### 4.2 CorrectiveAction

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| IncidentId | int (FK -> Incident) | yes | Parent incident in same organization. |
| Description | string(1000) | yes | Non-empty, max 1000 characters. |
| AssignedToId | string (FK -> AppUser) | yes | Active user in same organization; cannot be a deactivated user. |
| DueDate | date | yes | Must be >= date of creation. |
| Status | enum | auto | `Open`, `InProgress`, `Done`, `Overdue`; default `Open`; transitions per FR-SAF-10. |

### 4.3 ToolboxTalk

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| ProjectId | int (FK -> Project) | yes | Active project in caller's organization. |
| Topic | string(200) | yes | Non-empty, max 200 characters. |
| TalkDate | date | yes | Not before organization creation; not more than 90 days in the future. |
| DurationMinutes | int | yes | Range 5-480. |
| FacilitatorId | string (FK -> AppUser) | yes | Active user in same organization; defaults to caller. |
| Notes | string(4000) | no | Max 4000 characters. |

### 4.4 ToolboxAttendance

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| ToolboxTalkId | int (FK -> ToolboxTalk) | yes | Parent talk; unique with `WorkerId`. |
| WorkerId | int (FK -> Worker) | yes | Active worker in same organization; duplicates rejected with a validation message. |

## 5. Workflow state machines

### 5.1 Incident

| From | To | Allowed roles | Side effects |
|---|---|---|---|
| Draft | Reported | Reporter, SiteManager, SafetyOfficer, Admin | Audit `Submit`; notification to project SafetyOfficer. |
| Draft | (discarded) | Reporter, Admin | Hard delete + audit `Discard` (FR-SAF-08). |
| Reported | Investigating | SafetyOfficer, Admin | Audit `Transition`; assignee notified. |
| Reported | Closed | Admin only | Requires `RootCause` when Severity High/Critical; sets `ReviewedById`. |
| Investigating | Resolved | SafetyOfficer, Admin | Requires `RootCause` when Severity High/Critical; sets `ReviewedById`; notifies reporter. |
| Investigating | Reported | SafetyOfficer, Admin | Step-back permitted once; audit `Transition`. |
| Resolved | Closed | SafetyOfficer, Admin | Sets `ReviewedById` if empty; notifies reporter. |
| Resolved | Investigating | SafetyOfficer, Admin | Reopen for further investigation; audit `Reopen`. |
| Closed | Investigating | Admin | Terminal-break; requires reason in audit `Detail`; audit `Reopen`. |

### 5.2 CorrectiveAction

| From | To | Allowed roles | Side effects |
|---|---|---|---|
| Open | InProgress | Assignee, SafetyOfficer, Admin | Audit `Transition`. |
| Open | Overdue | System (nudge engine) or any authorized reader triggering recalculation | Included in dashboard KPI; notification to assignee. |
| InProgress | Done | Assignee, SafetyOfficer, Admin | Audit `Transition`; notifies incident reviewer. |
| InProgress | Overdue | System | As above. |
| Overdue | InProgress | Assignee, SafetyOfficer, Admin | Audit `Transition`. |
| Overdue | Done | Assignee, SafetyOfficer, Admin | Audit `Transition`; clears overdue KPI. |
| Done | (terminal) | - | No transitions. |

## 6. Validation rules (consolidated)

| ID | Rule | Applies to | Message key |
|---|---|---|---|
| V-SAF-01 | `OccurredAt` is not in the future (5-minute tolerance). | Incident | `incident.occurredat.future` |
| V-SAF-02 | `Title` non-empty and <= 200 chars. | Incident | `incident.title.required` |
| V-SAF-03 | `Location` non-empty. | Incident | `incident.location.required` |
| V-SAF-04 | `Description` present when Severity is High/Critical. | Incident | `incident.description.required` |
| V-SAF-05 | `RootCause` present before Resolved/Closed for Severity High/Critical. | Incident | `incident.rootcause.required` |
| V-SAF-06 | `AssignedToId` and `DueDate` present; DueDate >= today. | CorrectiveAction | `action.duedate.past` |
| V-SAF-07 | Duplicate `(ToolboxTalkId, WorkerId)` rejected. | ToolboxAttendance | `attendance.duplicate` |
| V-SAF-08 | `DurationMinutes` within 5-480. | ToolboxTalk | `talk.duration.range` |
| V-SAF-09 | `ProjectId` resolves inside caller's organization; otherwise 404-equivalent not-found (no existence leak). | All | `project.notfound` |
| V-SAF-10 | Role of target user is one of the six defined roles when assigned. | CorrectiveAction | `user.role.invalid` |

## 7. Acceptance criteria

| ID | Given | When | Then |
|---|---|---|---|
| AC-SAF-01 | A SiteManager in organization A creates a valid incident draft | The record is saved | `Reference` = `SIN-0001` for a new organization, status = `Draft`, `ReportedById` = caller, `OrganizationId` stamped server-side. |
| AC-SAF-02 | A SafetyOfficer opens an incident in `Reported` status | They attempt transition directly to `Closed` | The transition is rejected with `incident.transition.invalid`; status remains `Reported`; no partial data is written. |
| AC-SAF-03 | An incident with Severity `Critical` has no `RootCause` | SafetyOfficer attempts transition to `Resolved` | Validation `incident.rootcause.required` is returned; status remains `Investigating`. |
| AC-SAF-04 | A corrective action has `DueDate` = yesterday and status `InProgress` | The nudge engine runs (or dashboard loads) | The action is reported as overdue, appears in the "Overdue corrective actions" KPI, and the assignee receives one notification (not duplicated on subsequent runs). |
| AC-SAF-05 | A toolbox talk exists with three workers selected for attendance | The user saves attendance twice with the same worker in the selection | The second save is rejected with `attendance.duplicate`; exactly three `ToolboxAttendance` rows exist. |
| AC-SAF-06 | A Worker belonging to organization B requests incident `SIN-0002` belonging to organization A | The request is processed | The response is not-found (no data leak), and no audit row is created for organization A. |
| AC-SAF-07 | A QCInspector attempts to transition an incident to `Closed` | The request is processed | Authorization fails server-side; a `Denied` audit entry is written for organization A with entity id and action. |
| AC-SAF-08 | A `Closed` incident is reopened | An Admin submits a reopen reason | Status becomes `Investigating`, `ReviewedById` is retained until re-review, and an audit `Reopen` entry stores the reason. |

## 8. Non-functional notes

- **Tenancy:** every query in this module goes through the shared `OrganizationId` query filter; incident lists must never require the caller to pass `OrganizationId` explicitly.
- **Performance:** incident list (50 rows, filtered) p95 <= 400 ms server time; supported by indexes on `(OrganizationId, Status)`, `(OrganizationId, ProjectId)`, `(OrganizationId, Reference)`.
- **Concurrency:** state transitions use an optimistic check (expected status in request); a conflicting transition returns a conflict message showing the current status.
- **Notifications de-duplication:** nudge notifications are keyed by `(UserId, EntityType, EntityId, Action)` within a 24-hour window.
- **Audit retention:** audit rows are never deleted with the entity; discarding a draft keeps the audit trail.
- **Usability:** forms are single-column on mobile widths; severity is visually distinct but color is never the only signal (accessible contrast).
- **Related NFRs:** authorization matrix and auditability requirements are in `docs/requirements/NFRs.md` sections 3 and 5.
