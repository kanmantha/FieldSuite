# SRS - Module 2: Quality / QC

| Field | Value |
|---|---|
| Document ID | FS-SRS-QC |
| Version | 1.0 |
| Date | 2026-10-06 |
| Scope | Inspections with checklists, snag list with retest closure, non-conformance reports (NCR) |
| Parent document | PRD (FS-PRD-001), section 5 (M2) |

---

## 1. Purpose

This specification defines the functional and data requirements for the Quality/QC module: structured inspections (internal, client, third-party) with per-item results, a snag/defect list with retest-based closure, and non-conformance reports with containment tracking. The module supplies the "Open snags" dashboard KPI and the QC evidence described in PRD section 4.4.

## 2. Actors and roles

| Role | Module capability summary |
|---|---|
| Admin | Full control of all quality records including reassignment, forced closure, and reopening of closed snags/NCRs. |
| SiteManager | Create and edit inspections and snags for assigned projects; assign snags to workers/teams; view NCRs; may not close snags or NCRs. |
| SafetyOfficer | Read-only across the module; may raise an NCR arising from a safety observation. |
| QCInspector | Primary actor: runs inspections, adds checklist items, sets inspection outcomes, verifies retests, closes snags, owns NCR lifecycle. |
| Estimator | Read-only on inspection and snag lists (informative for pricing rework); no edits. |
| Worker | Raise snags (defects found while working) in `Open` status; view snags assigned to them; add retest notes when requested; no closure rights. |

## 3. Functional requirements

### 3.1 Inspections

| ID | Requirement |
|---|---|
| FR-QC-01 | **List inspections.** The system shall list inspections for the caller's organization with server-side pagination (default 50) and filters for Project, Type (`Internal`/`Client`/`ThirdParty`), Status, InspectionDate range, Inspector, and free-text search on Title/Reference. |
| FR-QC-02 | **Create inspection.** An authorized user shall be able to create an inspection with Project, Type, Title, InspectionDate, InspectorId (defaults to caller), OverallNotes; new records start in status `Draft` and receive `Reference` `QIN-NNNN` per FR-QC-03. |
| FR-QC-03 | **Automatic reference.** `Reference` shall be assigned as `QIN-` + 4-digit sequence per organization (`QIN-0001` first), allocated in the insert transaction, unique with `OrganizationId`, immutable. |
| FR-QC-04 | **Checklist items.** Authorized users shall be able to add, edit, and remove `InspectionItem` rows while the inspection is `Draft` or `InProgress`; each item has `ChecklistText` (required), `Result` (`Pass`/`Fail`/`N_A`, optional until the inspection is completed), and `Comment` (optional). |
| FR-QC-05 | **State machine (inspection).** Allowed transitions: `Draft -> InProgress`, `Draft -> Passed`, `InProgress -> Passed`, `InProgress -> Failed`, `Failed -> InProgress` (re-inspection), `Passed -> InProgress` (Admin only, re-open). Transition to `Passed` is permitted only when at least one item exists and every item has `Result` of `Pass` or `N_A`; transition to `Failed` is permitted when at least one item has `Result = Fail`. `Passed` and `Failed` are otherwise terminal. |
| FR-QC-06 | **Generate snags from failures.** From an inspection with at least one `Fail` item, an authorized user shall be able to raise a Snag per failed item in one action, pre-filling Title (`Inspection reference + checklist text`), Location, RaisedById, and `Priority` defaulting to `Medium`. |
| FR-QC-07 | **Validation (inspection).** The system shall enforce section 6.1 rules before persisting, including `InspectionDate` not in the future, Title length, and result consistency on completion. |
| FR-QC-08 | **Immutability.** `Reference` is never editable; inspection records past `Passed`/`Failed` are read-only except `OverallNotes` by QCInspector/Admin, each edit audited. |

### 3.2 Snags

| ID | Requirement |
|---|---|
| FR-QC-09 | **Create snag.** SiteManager, QCInspector, Worker, and Admin shall be able to create a snag with Project, Title, Description, Location, Priority; `RaisedById` = caller; optional `AssignedToId`, `DueDate`, and `PhotoPath` (image upload, see FR-QC-11); status starts `Open`; `Reference` = `SNG-NNNN` per FR-QC-10. |
| FR-QC-10 | **Automatic reference.** `Reference` shall be `SNG-` + 4-digit per-organization sequence, immutable, unique with `OrganizationId`. |
| FR-QC-11 | **Photo attachment.** A snag may carry one photo (`PhotoPath`); accepted types `image/jpeg`, `image/png`, `image/webp`; max 5 MB; stored under the tenant-scoped upload path; the file name is generated server-side (no client path traversal). |
| FR-QC-12 | **State machine (snag).** Allowed transitions: `Open -> Assigned`, `Open -> Rejected`, `Assigned -> InProgress`, `Assigned -> Rejected`, `InProgress -> Retest`, `Retest -> Closed`, `Retest -> InProgress`, `Retest -> Rejected`, `Assigned -> Open` (unassign). `Rejected` and `Closed` are terminal. `Closed` requires the caller to be QCInspector or Admin and stamps `ClosedAt`; `Rejected` requires QCInspector/Admin and a reason stored in the audit detail. |
| FR-QC-13 | **Assignment validation.** Transition `Open -> Assigned` requires a non-null `AssignedToId` in the same organization; transition `Assigned -> InProgress` may only be performed by the assignee, SiteManager, QCInspector, or Admin. |
| FR-QC-14 | **Aging and due-date nudges.** `INudgeEngine` shall surface snags that are overdue (`DueDate` < today and status not `Closed`/`Rejected`) or aging (status `Open`/`Assigned`/`InProgress`/`Retest` for more than 14 days), feeding the "Open snags" dashboard KPI and notifying the raiser and assignee. |
| FR-QC-15 | **Validation (snag).** Section 6.2 rules enforced server-side, including Priority required, Title length, DueDate >= RaisedAt date, and photo type/size limits. |

### 3.3 Non-conformance reports (NCR)

| ID | Requirement |
|---|---|
| FR-QC-16 | **Create NCR.** QCInspector, SafetyOfficer, and Admin shall be able to create an NCR with Project, Title, Severity, Description, DetectedDate, ContainmentAction; status starts `Open`; `Reference` = `NCR-NNNN` per FR-QC-17. |
| FR-QC-17 | **Automatic reference.** `Reference` shall be `NCR-` + 4-digit per-organization sequence, immutable, unique with `OrganizationId`. |
| FR-QC-18 | **State machine (NCR).** Allowed transitions: `Open -> Containment`, `Containment -> Investigation`, `Investigation -> Containment` (containment reopened), `Investigation -> Closed`, `Containment -> Closed` (Admin only). `Closed` is terminal. Transition to `Closed` requires non-empty `ContainmentAction` and a closure note in `Description` append or audit detail. |
| FR-QC-19 | **Edit NCR.** `Open` and `Containment` NCRs may be edited by QCInspector and Admin; `Investigation` and `Closed` NCRs are read-only except `ContainmentAction` by QCInspector/Admin (audited). |

### 3.4 Cross-cutting

| ID | Requirement |
|---|---|
| FR-QC-20 | **Notifications.** Create in-app notifications for: snag assigned, snag due/aging nudge, snag closed (to raiser), inspection completed as `Failed` (to SiteManager and QCInspector), NCR transitioned to `Closed` (to SiteManager). |
| FR-QC-21 | **Audit log.** Create, update, and every state transition of Inspection, InspectionItem, Snag, and NCR writes an `AuditLog` row with entity id, action, and field summary. |
| FR-QC-22 | **Dashboard KPI.** The module feeds "Open snags" = snags with status not in (`Closed`, `Rejected`) for the caller's organization. |
| FR-QC-23 | **Permissions enforcement.** All rules in section 2 are enforced server-side; unauthorized attempts return the standard authorization error and write a `Denied` audit entry. |

## 4. Data dictionary

### 4.1 Inspection

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| ProjectId | int (FK -> Project) | yes | Active project in caller's organization. |
| Reference | string(16) | auto | `QIN-NNNN` per organization; immutable; unique with `OrganizationId`. |
| Type | enum | yes | `Internal`, `Client`, `ThirdParty`. |
| Title | string(200) | yes | Non-empty, max 200 characters. |
| InspectionDate | date | yes | Not in the future. |
| InspectorId | string (FK -> AppUser) | yes | Active user in same organization; defaults to caller. |
| Status | enum | auto | `Draft`, `InProgress`, `Passed`, `Failed`; default `Draft`. |
| OverallNotes | string(4000) | no | Max 4000 characters. |

### 4.2 InspectionItem

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| InspectionId | int (FK -> Inspection) | yes | Parent inspection in same organization. |
| ChecklistText | string(500) | yes | Non-empty, max 500 characters. |
| Result | enum | no | `Pass`, `Fail`, `N_A`; nullable while inspection is `Draft`/`InProgress`; required for completion per FR-QC-05. |
| Comment | string(1000) | no | Required when `Result = Fail` (defect description). |

### 4.3 Snag

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| ProjectId | int (FK -> Project) | yes | Active project in caller's organization. |
| Reference | string(16) | auto | `SNG-NNNN` per organization; immutable; unique with `OrganizationId`. |
| Title | string(200) | yes | Non-empty, max 200 characters. |
| Description | string(2000) | no | Max 2000 characters; required when Priority is `High`/`Critical`. |
| Location | string(300) | yes | Non-empty. |
| Priority | enum | yes | `Low`, `Medium`, `High`, `Critical`. |
| RaisedById | string (FK -> AppUser) | auto | Caller at creation; immutable. |
| AssignedToId | string? (FK -> AppUser) | no | Required for status `Assigned`+; same organization; active user. |
| DueDate | date? | no | Must be >= date snag was raised. |
| PhotoPath | string(500)? | no | Server-generated tenant-scoped path; validated type/size (FR-QC-11). |
| Status | enum | auto | `Open`, `Assigned`, `InProgress`, `Retest`, `Closed`, `Rejected`; default `Open`. |
| ClosedAt | datetime? | no | Set only on transition to `Closed`; never cleared. |

### 4.4 NCR

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| ProjectId | int (FK -> Project) | yes | Active project in caller's organization. |
| Reference | string(16) | auto | `NCR-NNNN` per organization; immutable; unique with `OrganizationId`. |
| Title | string(200) | yes | Non-empty, max 200 characters. |
| Severity | enum | yes | `Low`, `Medium`, `High`, `Critical`. |
| Description | string(4000) | yes | Non-empty; describes the non-conformance. |
| DetectedDate | date | yes | Not in the future. |
| ContainmentAction | string(2000) | conditional | Required before transition to `Closed`. |
| Status | enum | auto | `Open`, `Containment`, `Investigation`, `Closed`; default `Open`. |

## 5. Workflow state machines

### 5.1 Inspection

| From | To | Allowed roles | Preconditions |
|---|---|---|---|
| Draft | InProgress | QCInspector, SiteManager, Admin | At least one InspectionItem exists. |
| Draft | Passed | QCInspector, Admin | >= 1 item; no `Fail` results. |
| InProgress | Passed | QCInspector, Admin | All items have `Result`; no `Fail`; audit `Complete`. |
| InProgress | Failed | QCInspector, Admin | >= 1 item with `Result = Fail`; notifies SiteManager (FR-QC-20). |
| Failed | InProgress | QCInspector, Admin | Re-inspection; new items may be added. |
| Passed | InProgress | Admin only | Re-open; audit `Reopen`. |

### 5.2 Snag

| From | To | Allowed roles | Preconditions |
|---|---|---|---|
| Open | Assigned | SiteManager, QCInspector, Admin | `AssignedToId` present (FR-QC-13). |
| Assigned | InProgress | Assignee, SiteManager, QCInspector, Admin | - |
| InProgress | Retest | Assignee, SiteManager, QCInspector, Admin | Fix claimed complete; audit `SubmitRetest`. |
| Retest | Closed | QCInspector, Admin | `ClosedAt` stamped; notifies raiser. |
| Retest | InProgress | QCInspector, Admin | Retest failed; audit `RetestFailed`. |
| Assigned/Retest/Open/InProgress | Rejected | QCInspector, Admin | Reason required in audit detail. |
| Assigned | Open | SiteManager, QCInspector, Admin | Unassign; `AssignedToId` cleared. |

### 5.3 NCR

| From | To | Allowed roles | Preconditions |
|---|---|---|---|
| Open | Containment | QCInspector, SafetyOfficer, Admin | `ContainmentAction` non-empty. |
| Containment | Investigation | QCInspector, Admin | - |
| Investigation | Containment | QCInspector, Admin | Recurrence/failed fix. |
| Investigation | Closed | QCInspector, Admin | `ContainmentAction` non-empty; audit `Close`. |
| Containment | Closed | Admin only | Exception path; audit `Close`. |

## 6. Validation rules (consolidated)

| ID | Rule | Applies to |
|---|---|---|
| V-QC-01 | `InspectionDate` and `DetectedDate` not in the future. | Inspection, NCR |
| V-QC-02 | `Title` non-empty and <= 200 characters. | Inspection, Snag, NCR |
| V-QC-03 | Completion requires every item to have a `Result`; `Passed` requires zero `Fail`. | Inspection |
| V-QC-04 | `Comment` required when item `Result = Fail`. | InspectionItem |
| V-QC-05 | `Priority` required; `Description` required for High/Critical snags. | Snag |
| V-QC-06 | `DueDate` (when present) >= date snag raised. | Snag |
| V-QC-07 | Photo <= 5 MB, type in (`image/jpeg`, `image/png`, `image/webp`); path generated server-side under tenant folder. | Snag |
| V-QC-08 | `AssignedToId` required for status `Assigned` and beyond; user must be active and in same organization. | Snag |
| V-QC-09 | `ContainmentAction` non-empty before `Closed`. | NCR |
| V-QC-10 | `ProjectId` resolves inside caller's organization; otherwise not-found with no existence leak. | All |

## 7. Acceptance criteria

| ID | Given | When | Then |
|---|---|---|---|
| AC-QC-01 | A QCInspector creates a new inspection for an active project | The record saves | `Reference` = `QIN-0001` for a new organization, status = `Draft`, `InspectorId` = caller, `OrganizationId` stamped server-side. |
| AC-QC-02 | An `InProgress` inspection has three items: Pass, N_A, and one item with no result | The inspector attempts to mark it `Passed` | The transition is rejected with `inspection.completion.items` requiring a result on all items; status stays `InProgress`. |
| AC-QC-03 | An `InProgress` inspection has one item with `Result = Fail` and a comment | The inspector completes it | Status becomes `Failed`; SiteManager and QCInspector each receive one notification; audit `Complete` is written. |
| AC-QC-04 | A snag in `Retest` passes verification | The QCInspector closes it | Status = `Closed`, `ClosedAt` is set to server time, the raiser is notified, and the snag leaves the "Open snags" KPI. |
| AC-QC-05 | A Worker attempts to transition a snag from `InProgress` to `Closed` | The request is processed | Server-side authorization fails, status is unchanged, and a `Denied` audit entry is written. |
| AC-QC-06 | An NCR in `Investigation` with empty `ContainmentAction` | Admin attempts `Closed` | Validation `ncr.containment.required` is returned; status remains `Investigation`. |
| AC-QC-07 | A user uploads a 7 MB PDF as a snag photo | The upload is submitted | The upload is rejected with `snag.photo.type`/size message; no file is written. |
| AC-QC-08 | A snag has been `Open` for 16 days | The nudge engine runs | The snag appears in aging nudges, the raiser and project SiteManager are notified once per 24 hours, and the "Open snags" KPI includes it. |

## 8. Non-functional notes

- **Tenancy:** all quality queries pass through the shared `OrganizationId` filter; uploaded photos are stored under a per-organization directory and served through an authorized handler (never by raw guessable path).
- **Performance:** inspection and snag list p95 <= 400 ms; snag KPI computed by indexed count, not by scanning; photo upload p95 <= 3 s for 5 MB on a 10 Mbps uplink.
- **Data integrity:** inspection completion and item results are written in one transaction; `ClosedAt` is set exactly once.
- **Usability:** checklist entry supports keyboard-only completion and tap targets >= 44 px on mobile; result state is conveyed by text plus icon, not color alone.
- **Print/export:** inspection detail and snag list provide print-friendly views (browser print CSS), consistent with the printable proposal pattern in Module 4.
- **Related NFRs:** authorization matrix and auditability are in `docs/requirements/NFRs.md` sections 3 and 5.
