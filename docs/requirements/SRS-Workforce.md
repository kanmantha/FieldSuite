# SRS - Module 5: Workforce

| Field | Value |
|---|---|
| Document ID | FS-SRS-WKF |
| Version | 1.0 |
| Date | 2026-10-06 |
| Scope | Contractor companies, workers, attendance, onboarding documents with expiry flags |
| Parent document | PRD (FS-PRD-001), section 5 (M5) |

---

## 1. Purpose

This specification defines the functional and data requirements for the Workforce module: the contractor and worker directory, daily attendance capture with computed hours, onboarding document registers with 30-day expiry flagging, and the attendance-versus-active-permits cross-check. The module feeds the "Expiring documents" and "Today's attendance" dashboard KPIs (PRD sections 4.2 and 4.6).

## 2. Actors and roles

| Role | Module capability summary |
|---|---|
| Admin | Full control: manage contractor companies and workers (including deactivation), override attendance, manage all documents, view audit. |
| SiteManager | Primary actor for attendance: record/edit attendance for projects they manage; create and edit worker records; upload documents; view contractor list. |
| SafetyOfficer | View workers and documents; receive and act on document expiry nudges; upload safety certificates; no payroll-relevant edits. |
| QCInspector | Read-only worker directory (for snag assignment context). |
| Estimator | Read-only worker directory with contact PII masked (name, job title, shift, contractor only) for crew/rate context. |
| Worker | View own profile, own attendance history, and own documents; may not edit any of them. |

## 3. Functional requirements

### 3.1 Contractor companies

| ID | Requirement |
|---|---|
| FR-WKF-01 | **Create contractor.** Admin and SiteManager shall be able to create a `ContractorCompany` with Name, ContactPerson, Phone, Email, Trade; `IsActive` defaults to `true`. |
| FR-WKF-02 | **Edit contractor.** Admin and SiteManager may edit contractor fields at any time; Admin may set `IsActive = false` (deactivate). Deactivation hides the contractor from new-worker selection but keeps all historical worker, attendance, and toolbox records intact and visible. |
| FR-WKF-03 | **List contractors.** The system shall list contractors for the organization with filters for Trade, `IsActive`, and free-text search on Name/ContactPerson, paginated (default 50). |

### 3.2 Workers

| ID | Requirement |
|---|---|
| FR-WKF-04 | **Create worker.** Admin and SiteManager shall be able to create a `Worker` with FullName, JobTitle, Phone, Email, HireDate, Shift (`Morning`/`Day`/`Night`), optional ContractorCompanyId (active contractor in same organization); `IsActive` defaults to `true`; `OrganizationId` stamped server-side. |
| FR-WKF-05 | **Edit/deactivate worker.** Admin and SiteManager may edit worker fields; Admin or SiteManager may set `IsActive = false`. Deactivated workers cannot be selected for new attendance, new toolbox attendance, or new document records, but remain on all historical records. |
| FR-WKF-06 | **List workers.** The system shall list workers with filters for ContractorCompany, Shift, `IsActive`, JobTitle, and free-text search on FullName/Phone, paginated (default 50); Estimator receives the masked projection defined in section 2. |
| FR-WKF-07 | **Worker detail.** The worker detail shows profile, latest attendance (last 30 days), documents with expiry status, and linked toolbox talk attendance count. |

### 3.3 Attendance

| ID | Requirement |
|---|---|
| FR-WKF-08 | **Record attendance.** Admin and SiteManager shall be able to record attendance for a project and `WorkDate` in a single bulk save: select multiple active workers and set `Status` per worker (`Present`/`Absent`/`Late`/`OnLeave`), with optional `ClockIn`, `ClockOut`, and `Notes`. New rows default to `Present` when a clock time is supplied. |
| FR-WKF-09 | **Hours computation.** When both `ClockIn` and `ClockOut` are present, `HoursWorked` is computed server-side as `(ClockOut - ClockIn)` in hours rounded to 2 dp; if only `Status` is set (e.g. `Absent`, `OnLeave`), `HoursWorked` remains null; an authorized user may override the computed value, which is recorded in audit detail. |
| FR-WKF-10 | **Uniqueness.** Only one attendance row may exist per `(WorkerId, WorkDate)`; a duplicate save returns `attendance.duplicate` and no partial rows are written (transactional bulk save). |
| FR-WKF-11 | **Edit attendance.** Admin and SiteManager may edit attendance rows for today or earlier dates until the project's attendance for that date is confirmed by Admin (confirm flag is implied by Admin edit lock: Admin can always edit; SiteManager can edit rows for the last 7 days only). Every edit writes an audit entry. |
| FR-WKF-12 | **Attendance views.** The system shall provide: (a) day sheet by project (workers x status for a `WorkDate`), (b) per-worker attendance history, (c) monthly summary of days present and total `HoursWorked` per worker, all organization-scoped and paginated/printable. |
| FR-WKF-13 | **Today's attendance KPI.** The dashboard tile "Today's attendance" shows, for the current server date: `Present + Late` count, total active workers, and percentage, scoped to the organization. |
| FR-WKF-14 | **Permit cross-check.** The day sheet shall display, for the selected project and date, the permits in status `Active` covering that date; it shall flag mismatches in both directions: (a) attendance rows on a project/date with zero `Active` permits, and (b) `Active` permits with zero present workers. Flags are informational (no hard block) and link to the permit detail (Module 3 FR-PTW-25). |

### 3.4 Onboarding documents

| ID | Requirement |
|---|---|
| FR-WKF-15 | **Create document record.** Admin, SiteManager, and SafetyOfficer shall be able to add an `OnboardingDocument` for a worker with Type (`ID`/`Contract`/`SafetyCert`/`Medical`/`Insurance`), optional DocumentNumber, optional IssuedDate, optional ExpiryDate, optional FilePath (file upload permitted types: `application/pdf`, `image/jpeg`, `image/png`; max 10 MB; server-generated tenant-scoped path). |
| FR-WKF-16 | **Expiry flagging.** The system shall classify each document on every read: `Expired` (ExpiryDate < today), `ExpiringSoon` (0 <= ExpiryDate - today <= 30 days), `Valid`, or `NoExpiry` (ExpiryDate null). Classification is computed, never stored. |
| FR-WKF-17 | **Expiring documents KPI and nudge.** The dashboard tile "Expiring documents" counts documents in `Expired` + `ExpiringSoon` for active workers; `INudgeEngine` notifies SafetyOfficer and Admin once per document per 7 days while it remains within 30 days of expiry (and once on the expiry date for `Expired` documents lacking a replacement). |
| FR-WKF-18 | **Edit/delete document.** Admin and SafetyOfficer may edit metadata and replace the file; Admin may delete a document record (audited, including file name). Worker records with documents are never deleted. |
| FR-WKF-19 | **Document listing.** A register view lists documents by worker with filters for Type and expiry classification, sorted by soonest expiry first, paginated; workers with at least one `Expired` document are marked in the list. |

### 3.5 Cross-cutting

| ID | Requirement |
|---|---|
| FR-WKF-20 | **Notifications.** Create in-app notifications for: document expiring/expired (SafetyOfficer, Admin), worker deactivated (Admin), and attendance anomaly flags (SiteManager) when the permit cross-check reports a mismatch. |
| FR-WKF-21 | **Audit log.** Create/update/deactivate of ContractorCompany and Worker, every attendance create/edit, and document create/replace/delete write `AuditLog` rows with field summaries. |
| FR-WKF-22 | **Permissions enforcement.** Section 2 role rules are enforced server-side (including the Estimator masked projection); denied attempts return the standard authorization error and a `Denied` audit entry. |
| FR-WKF-23 | **Validation rules.** Section 6.1 rules enforced server-side on all creates and updates. |

## 4. Data dictionary

### 4.1 ContractorCompany

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| OrganizationId | int (FK -> Organization) | auto | Stamped server-side; indexed. |
| Name | string(200) | yes | Non-empty, max 200; unique case-insensitive within organization (warning-level duplicate check). |
| ContactPerson | string(100) | yes | Non-empty. |
| Phone | string(30) | yes | Non-empty; digits/spaces/`+`/`-`/`()` only. |
| Email | string(100)? | no | Valid email format when present. |
| Trade | string(100) | yes | Free text (e.g. `Electrical`, `Scaffolding`, `Civil`). |
| IsActive | bool | yes | Default `true`; deactivation is non-destructive (FR-WKF-02). |

### 4.2 Worker

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| OrganizationId | int (FK -> Organization) | auto | Stamped server-side; indexed. |
| ContractorCompanyId | int? (FK -> ContractorCompany) | no | Must be an active contractor in the same organization when provided. |
| FullName | string(150) | yes | Non-empty, max 150 characters. |
| JobTitle | string(100) | yes | Non-empty. |
| Phone | string(30)? | no | Format rule as in 4.1 when present. |
| Email | string(100)? | no | Valid email format when present. |
| HireDate | date | yes | Not in the future; not before organization creation. |
| Shift | enum | yes | `Morning`, `Day`, `Night`. |
| IsActive | bool | yes | Default `true`; deactivation blocks new attendance/toolbox/document rows. |

### 4.3 Attendance

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| WorkerId | int (FK -> Worker) | yes | Active worker in same organization (at time of record); unique with `WorkDate`. |
| ProjectId | int (FK -> Project) | yes | Active project in same organization. |
| WorkDate | date | yes | Not in the future; unique with `WorkerId`. |
| ClockIn | datetime? | no | Not after `ClockOut` when both present; not after server time. |
| ClockOut | datetime? | no | > `ClockIn`; not after server time. |
| HoursWorked | decimal(6,2)? | computed/optional | Computed per FR-WKF-09; manual override audited; 0-24 range. |
| Status | enum | yes | `Present`, `Absent`, `Late`, `OnLeave`; required on every row. |
| Notes | string(500)? | no | Max 500 characters. |

### 4.4 OnboardingDocument

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| WorkerId | int (FK -> Worker) | yes | Worker in same organization. |
| Type | enum | yes | `ID`, `Contract`, `SafetyCert`, `Medical`, `Insurance`. |
| DocumentNumber | string(100)? | no | Free text identifier. |
| IssuedDate | date? | no | Not in the future. |
| ExpiryDate | date? | no | >= `IssuedDate` when both present; drives FR-WKF-16 classification. |
| FilePath | string(500)? | no | Server-generated tenant-scoped path; type/size validated (FR-WKF-15). |

## 5. Workflow / lifecycle rules

| Subject | Rule |
|---|---|
| Contractor lifecycle | `Active -> Inactive` by Admin; inactive contractors cannot be selected for new workers; existing workers keep the FK and history remains queryable. |
| Worker lifecycle | `Active -> Inactive` by Admin/SiteManager; blocks new attendance, toolbox attendance, and document rows; historical rows are immutable; reactivation is unrestricted. |
| Attendance day lifecycle | Rows editable by SiteManager for the last 7 days and by Admin for any past date; `WorkDate` cannot be in the future; the day sheet is final for payroll export only after Admin edit lock (informational in v1, no separate flag column). |
| Document lifecycle | Created -> Valid -> ExpiringSoon -> Expired (computed); replacement = new file upload on the same row (audited) or a new row of the same type (older row deleted by Admin only). |

## 6. Validation rules (consolidated)

| ID | Rule | Message key |
|---|---|---|
| V-WKF-01 | `HireDate` not in the future; `IssuedDate` not in the future. | `worker.hiredate.future` |
| V-WKF-02 | `ExpiryDate` >= `IssuedDate` when both present. | `document.expiry.before.issue` |
| V-WKF-03 | `ClockOut` > `ClockIn`; neither after server time. | `attendance.clock.range` |
| V-WKF-04 | One attendance row per `(WorkerId, WorkDate)`; bulk save is all-or-nothing. | `attendance.duplicate` |
| V-WKF-05 | `WorkDate` not in the future. | `attendance.date.future` |
| V-WKF-06 | `ContractorCompanyId` resolves to an active contractor in the same organization. | `contractor.notfound` |
| V-WKF-07 | File types limited to `application/pdf`, `image/jpeg`, `image/png`; max 10 MB; server-generated path. | `document.file.invalid` |
| V-WKF-08 | Only active workers selectable for new attendance/toolbox/document rows. | `worker.inactive` |
| V-WKF-09 | `FullName`, `JobTitle`, `Trade`, `ContactPerson` non-empty within max lengths. | `workforce.required` |
| V-WKF-10 | `WorkerId`, `ProjectId` resolve inside caller's organization; otherwise not-found with no existence leak. | `project.notfound` |

## 7. Acceptance criteria

| ID | Given | When | Then |
|---|---|---|---|
| AC-WKF-01 | A SiteManager bulk-saves attendance for 12 workers on project P for today with 10 `Present` (clock in/out set) and 2 `Absent` | The save completes | Exactly 12 rows exist for that date, `HoursWorked` is computed for the 10 present rows and null for the 2 absent rows, no duplicates possible on re-submit. |
| AC-WKF-02 | A worker already has an attendance row dated 2026-10-06 and the same date is submitted again with a different status | The duplicate save is processed | `attendance.duplicate` is returned, the original row is unchanged, and no partial bulk rows are written. |
| AC-WKF-03 | A document has `ExpiryDate` = today + 20 days | The dashboard and document register load | The document is classified `ExpiringSoon`, included in the "Expiring documents" KPI, and SafetyOfficer/Admin receive one nudge (not repeated within 7 days). |
| AC-WKF-04 | A document has `ExpiryDate` = yesterday | The classification runs | Status shows `Expired`, the worker is marked in the register, and the KPI count includes it. |
| AC-WKF-05 | A project has 3 workers clocked in today but no `Active` permit for today | The day sheet loads | The mismatch flag (a) is displayed with a link to the project's permit list; no attendance data is altered. |
| AC-WKF-06 | An `Active` permit covers today on project P but zero workers are `Present` | The day sheet loads | The mismatch flag (b) is displayed linking to the permit; the SiteManager receives one anomaly notification. |
| AC-WKF-07 | A Worker requests the worker directory | The request is processed | Only their own profile/attendance/documents are returned; server-side authorization prevents directory access and writes a `Denied` audit entry. |
| AC-WKF-08 | An Estimator requests the worker list | The request is processed | The masked projection is returned (no Phone/Email); direct requests for another worker's contact details are denied. |
| AC-WKF-09 | A contractor with two linked workers is deactivated by Admin | The save completes | The contractor is hidden from new selections, both workers keep their FK and history remains visible, and an audit entry records the deactivation. |

## 8. Non-functional notes

- **Tenancy:** all workforce queries are organization-scoped; document files are stored under per-organization directories and served only through an authorized handler; contact PII masking for Estimator is enforced in the projection layer, not just the view.
- **Performance:** worker/attendance lists p95 <= 400 ms with pagination; bulk attendance save of 100 workers p95 <= 1.5 s; document expiry classification computed via indexed date queries, not full-table scans.
- **Data integrity:** bulk attendance save runs in a single transaction; uniqueness enforced by a composite unique index on `(WorkerId, WorkDate)`; computed `HoursWorked` and expiry classification are never client-authoritative.
- **Privacy:** worker contact details and documents are least-privilege: Worker sees only self; Estimator sees masked data; document file paths are not guessable and require an authenticated, authorized request.
- **Usability:** day sheet entry is optimized for repeat tapping (status toggle buttons per row, keyboard entry for clock times); document register sorts by soonest expiry so the most urgent items are first.
- **Related NFRs:** authorization matrix, privacy, backup/retention, and auditability are in `docs/requirements/NFRs.md` sections 3, 5, and 7.
