# SRS - Module 3: ePTW (Permits to Work)

| Field | Value |
|---|---|
| Document ID | FS-SRS-PTW |
| Version | 1.0 |
| Date | 2026-10-06 |
| Scope | Work permits, risk scoring, approval chain, checklists, activation, expiry |
| Parent document | PRD (FS-PRD-001), section 5 (M3) |

---

## 1. Purpose

This specification defines the functional and data requirements for the electronic Permit-to-Work module: creation of typed work permits, automatic risk scoring (`IPermitRiskScoring`), a role-tiered approval chain, per-type safety checklists, activation with actual start/end capture, suspension, closure, and automatic expiry. The module guarantees that no permit reaches `Active` without the approval levels its risk level requires (PRD success metric SM-4).

## 2. Actors and roles

| Role | Module capability summary |
|---|---|
| Admin | Full control: create/edit any permit, approve level-3 decisions, suspend/resume/close, force-expire, override with audit. |
| SiteManager | Primary requester: create and edit permits in `Draft`/`PendingApproval`, approve level-1 decisions, activate approved permits, close permits. |
| SafetyOfficer | Approve level-2 decisions, review risk score, suspend an `Active` permit, resume or close a suspended permit, reject at level 2. |
| QCInspector | Read-only on permits (quality hold interplay); may not approve or activate. |
| Estimator | Read-only on permit lists (site-condition context for pricing). |
| Worker | View permits for projects they are assigned to (read-only); complete checklist items only when explicitly assigned as checker; no approval rights. |

## 3. Functional requirements

### 3.1 Creation and risk scoring

| ID | Requirement |
|---|---|
| FR-PTW-01 | **Create permit.** SiteManager and Admin shall be able to create a permit with Project, Type, Title, Description, Location, RequestedStart, RequestedEnd, GasTestRequired, SpecialConditions; `RequestedById` = caller; status starts `Draft`; `Reference` = `PWT-NNNN` per FR-PTW-03. |
| FR-PTW-02 | **Risk scoring at creation.** On creation (and on any edit to Type, IsolationVerified, RequestedStart/RequestedEnd, or crew size while the permit is not yet `Active`), the system shall call `IPermitRiskScoring.Score(input)` where input = permit `Type`, `IsolationVerified`, duration hours (`RequestedEnd - RequestedStart`), and crew size supplied in the create/edit request. The service returns `RiskLevel` (`Low`/`Medium`/`High`/`Extreme`) and the required approval level count; both are persisted on the permit and in the audit detail. |
| FR-PTW-03 | **Automatic reference.** `Reference` shall be `PWT-` + 4-digit per-organization sequence (`PWT-0001` first), allocated in the insert transaction, unique with `OrganizationId`, immutable. |
| FR-PTW-04 | **Scoring input persistence.** Crew size is not a first-class column: it is supplied in the request DTO, used for scoring, and recorded as part of `AuditLog.Detail` at creation/refresh so the score can be reconstructed. The persisted `RiskLevel` is the authoritative value for the approval chain. |
| FR-PTW-05 | **Crew size default.** If the request omits crew size, the service shall default to 1 and record the defaulting in the audit detail. |
| FR-PTW-06 | **Approval chain generation.** On submission from `Draft` to `PendingApproval`, the system shall generate `PermitApproval` rows from `RiskLevel`: `Low` -> 1 level (`SiteManager`); `Medium` -> 2 levels (`SiteManager`, `SafetyOfficer`); `High`/`Extreme` -> 3 levels (`SiteManager`, `SafetyOfficer`, `Admin`). Rows are created with `Decision = Pending`, sequential `Level` starting at 1, and no `ApproverId`. |
| FR-PTW-07 | **Score refresh resets approvals.** If Type/isolation/dates/crew change while status is `Draft` or `PendingApproval`, the system shall rescore, and on submission delete and regenerate pending `PermitApproval` rows to match the new chain; decided rows for a permit that returns to `Draft` are cleared (history preserved in `AuditLog`). |

### 3.2 Approval workflow

| ID | Requirement |
|---|---|
| FR-PTW-08 | **Submit for approval.** The requester, SiteManager, or Admin shall be able to move `Draft -> PendingApproval` when all mandatory fields are valid (section 6.1); the system shall notify the level-1 approver role. |
| FR-PTW-09 | **Decide.** A user whose role matches the current level's `ApproverRole` (or Admin) shall be able to record `Approved` or `Rejected` with an optional `Comment`; the system shall set `ApproverId`, `Decision`, `DecidedAt`, and write an audit entry. Decisions on already-decided levels are forbidden. |
| FR-PTW-10 | **Sequential progression.** Only the lowest `Level` with `Decision = Pending` may be decided. On `Approved`: if higher pending levels exist, notify the next approver role; otherwise set permit status to `Approved`. On `Rejected`: permit status becomes `Rejected` (terminal), remaining pending levels are marked `Rejected` with comment "superseded", and the requester is notified. |
| FR-PTW-11 | **Approval integrity.** Permit status may only become `Approved` when every generated `PermitApproval` row has `Decision = Approved`; the check is performed inside the same transaction as the final decision. |

### 3.3 Checklist, activation, and lifecycle

| ID | Requirement |
|---|---|
| FR-PTW-12 | **Checklist template.** On creation the system shall seed `PermitChecklistItem` rows from a per-type template (e.g. Hot Work: fire watch confirmed, extinguisher present, gas test, isolation verified; ConfinedSpace: atmospheric test, attendant assigned, rescue plan). Items are editable while `Draft`; items may not be added after `PendingApproval`. |
| FR-PTW-13 | **Check off.** Authorized users (requester, SiteManager, SafetyOfficer, Admin, or a Worker when acting as the assigned checker) shall be able to toggle `IsChecked`; the system shall stamp `CheckedById` and `CheckedAt` on check and clear both on uncheck. |
| FR-PTW-14 | **Activation.** SiteManager, SafetyOfficer, or Admin shall be able to move `Approved -> Active` only when: (a) all `PermitChecklistItem` rows are checked; (b) `IsolationVerified = true` for isolation-relevant types (`HotWork`, `ConfinedSpace`, `Electrical`, `Excavation`); (c) when `GasTestRequired = true`, the atmospheric/gas-test checklist item is checked. The system sets `ActualStart` to server time (or the scheduled `RequestedStart` if earlier and within the window) and notifies requester and SafetyOfficer. |
| FR-PTW-15 | **Suspend and resume.** SafetyOfficer or Admin shall be able to move `Active -> Suspended` with a reason (audit + notify requester); `Suspended -> Active` (resume, SafetyOfficer/Admin) or `Suspended -> Closed` (SiteManager/SafetyOfficer/Admin). |
| FR-PTW-16 | **Close.** From `Active` or `Suspended`, SiteManager, SafetyOfficer, or Admin shall be able to close the permit; the system sets `ActualEnd` to server time (or `RequestedEnd` if already past) and makes the record read-only. |
| FR-PTW-17 | **Auto-expire.** A background job (hourly) shall set any non-terminal permit (`PendingApproval`, `Approved`, `Active`, `Suspended`) whose `RequestedEnd` is in the past to `Expired`. For an `Active` permit, `ActualEnd` is set to `RequestedEnd`. Expiry writes an audit entry and notifies the requester and SafetyOfficer. |
| FR-PTW-18 | **Expiry warning.** `INudgeEngine` shall notify the requester and SafetyOfficer once within 24 hours before `RequestedEnd` for permits in status `Approved` or `Active`. |
| FR-PTW-19 | **Rejection and draft editing.** `Rejected` and `Expired` are terminal; the requester or Admin may copy a rejected/expired permit into a new `Draft` (new reference), preserving the original untouched. |

### 3.4 Cross-cutting

| ID | Requirement |
|---|---|
| FR-PTW-20 | **Validation rules.** Section 6.1 rules are enforced server-side on create, edit, submit, and activation; all violations returned together as field-level messages. |
| FR-PTW-21 | **Permissions enforcement.** Section 2 role rules are enforced server-side; unauthorized attempts return the standard authorization error and a `Denied` audit entry. |
| FR-PTW-22 | **Notifications.** Create in-app notifications for: submitted (to level-1 approver), each approval step (to next approver role), approved (to requester), rejected (to requester), activation (to requester and SafetyOfficer), suspension (to requester), expiry warning (FR-PTW-18), expiry (FR-PTW-17). |
| FR-PTW-23 | **Audit log.** Create, edit, score refresh, submit, each decision, activation, suspension, resume, closure, and expiry write `AuditLog` rows capturing old/new status and risk level. |
| FR-PTW-24 | **Dashboard KPI.** The module feeds "Active permits" = permits with status `Active` in the caller's organization. |
| FR-PTW-25 | **Attendance cross-check.** The permit list/detail shall display the count of workers clocked in on the permit's project for the permit date, and the attendance screen shall flag days with attendance but no `Active` permit covering the work window (cross-module rule with Module 5, implemented via shared service). |

## 4. Data dictionary

### 4.1 WorkPermit

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| OrganizationId | int (FK -> Organization) | auto | Stamped server-side; indexed. |
| ProjectId | int (FK -> Project) | yes | Active project in caller's organization. |
| Reference | string(16) | auto | `PWT-NNNN` per organization; immutable; unique with `OrganizationId`. |
| Type | enum | yes | `HotWork`, `ConfinedSpace`, `Height`, `Electrical`, `Excavation`, `Lifting`. |
| Title | string(200) | yes | Non-empty, max 200 characters. |
| Description | string(4000) | no | Max 4000; required for `Extreme` risk before submission. |
| Location | string(300) | yes | Non-empty. |
| RequestedById | string (FK -> AppUser) | auto | Caller at creation; immutable. |
| RequestedStart | datetime | yes | Not before server time at creation (5-minute tolerance). |
| RequestedEnd | datetime | yes | Strictly greater than `RequestedStart`; max duration 30 days. |
| RiskLevel | enum | auto | `Low`, `Medium`, `High`, `Extreme`; produced by `IPermitRiskScoring`; immutable once `Active`. |
| Status | enum | auto | `Draft`, `PendingApproval`, `Approved`, `Active`, `Suspended`, `Closed`, `Rejected`, `Expired`; default `Draft`. |
| IsolationVerified | bool | yes | Default `false`; must be `true` for isolation-relevant types at activation (FR-PTW-14). |
| GasTestRequired | bool | yes | Default `false`; when `true`, gas-test checklist item must be checked at activation. |
| SpecialConditions | string(2000) | no | Free text (e.g. "Work permit valid for shift only"). |
| ActualStart | datetime? | no | Set at activation only. |
| ActualEnd | datetime? | no | Set at closure or expiry only. |

### 4.2 PermitChecklistItem

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| WorkPermitId | int (FK -> WorkPermit) | yes | Parent permit in same organization. |
| Text | string(500) | yes | Non-empty, max 500; editable only while `Draft`. |
| IsChecked | bool | yes | Default `false`; all must be `true` for activation. |
| CheckedById | string? (FK -> AppUser) | conditional | Set when checked; cleared on uncheck. |
| CheckedAt | datetime? | conditional | Set when checked; cleared on uncheck. |

### 4.3 PermitApproval

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| WorkPermitId | int (FK -> WorkPermit) | yes | Parent permit in same organization. |
| Level | int | yes | 1..3, sequential, unique with `WorkPermitId`. |
| ApproverRole | string | yes | `SiteManager`, `SafetyOfficer`, or `Admin` per FR-PTW-06. |
| ApproverId | string? (FK -> AppUser) | conditional | Set on decision; must have `ApproverRole` or be Admin. |
| Decision | enum | yes | `Pending`, `Approved`, `Rejected`; default `Pending`. |
| Comment | string(1000)? | no | Required when `Decision = Rejected`. |
| DecidedAt | datetime? | conditional | Set on decision; never cleared. |

### 4.4 Risk scoring contract (IPermitRiskScoring)

| Element | Definition |
|---|---|
| Input | Permit `Type`, `IsolationVerified` (bool), `DurationHours` (RequestedEnd - RequestedStart), `CrewSize` (int, default 1). |
| Output | `RiskLevel` + `RequiredApprovalLevels` (1-3) + human-readable rationale (stored in audit detail). |
| Rule basis (v1, rule-based) | Base level by type: `HotWork`, `ConfinedSpace` -> base High; `Height`, `Electrical`, `Excavation`, `Lifting` -> base Medium; escalate one level if `IsolationVerified = false`; escalate one level if duration > 8 hours or > 72 hours; escalate one level if crew size > 5; cap at `Extreme`, floor at `Low`. |
| Fallback | If the scoring service is unavailable, submission fails safe: permit remains `Draft` with a clear error (never silently defaults to `Low`). |

## 5. Workflow state machine (WorkPermit)

| From | To | Allowed roles | Preconditions / side effects |
|---|---|---|---|
| Draft | PendingApproval | Requester, SiteManager, Admin | Mandatory fields valid; approval rows generated (FR-PTW-06); notify level-1 approver. |
| PendingApproval | Draft | Requester, SiteManager, Admin | Withdraw; pending decisions cleared, history in audit. |
| PendingApproval | Approved | System (via final level approval) | All approval rows `Approved` (FR-PTW-11); notify requester. |
| PendingApproval | Rejected | Any level approver, Admin | Comment required; pending rows superseded; notify requester. |
| Approved | Active | SiteManager, SafetyOfficer, Admin | Checklist complete + isolation/gas conditions (FR-PTW-14); sets `ActualStart`. |
| Approved | Expired | System (expiry job) | `RequestedEnd` past. |
| Active | Suspended | SafetyOfficer, Admin | Reason required; notify requester. |
| Active | Closed | SiteManager, SafetyOfficer, Admin | Sets `ActualEnd`; read-only afterwards. |
| Active | Expired | System (expiry job) | `RequestedEnd` past; `ActualEnd = RequestedEnd`. |
| Suspended | Active | SafetyOfficer, Admin | Resume; audit `Resume`. |
| Suspended | Closed | SiteManager, SafetyOfficer, Admin | Sets `ActualEnd`. |
| Suspended | Expired | System (expiry job) | `RequestedEnd` past. |
| PendingApproval | Expired | System (expiry job) | `RequestedEnd` past. |
| Rejected / Expired / Closed | (terminal) | - | Only "copy to new draft" path (FR-PTW-19). |

## 6. Validation rules (consolidated)

| ID | Rule | Message key |
|---|---|---|
| V-PTW-01 | `RequestedEnd` > `RequestedStart`; duration <= 30 days. | `permit.range.invalid` |
| V-PTW-02 | `RequestedStart` not in the past at creation. | `permit.start.past` |
| V-PTW-03 | `Title` and `Location` non-empty, within max lengths. | `permit.required` |
| V-PTW-04 | `Type` is a defined enum value. | `permit.type.invalid` |
| V-PTW-05 | All mandatory fields valid before `Draft -> PendingApproval`. | `permit.submit.incomplete` |
| V-PTW-06 | Decision only by matching `ApproverRole` (or Admin) and only on the current pending level. | `permit.decision.forbidden` |
| V-PTW-07 | `Comment` required on `Rejected` decisions. | `permit.comment.required` |
| V-PTW-08 | Activation preconditions (checklist, isolation, gas test) all satisfied. | `permit.activate.blocked` |
| V-PTW-09 | `Description` required when `RiskLevel = Extreme` before submission. | `permit.description.required` |
| V-PTW-10 | `ProjectId` resolves inside caller's organization; otherwise not-found with no existence leak. | `project.notfound` |

## 7. Acceptance criteria

| ID | Given | When | Then |
|---|---|---|---|
| AC-PTW-01 | A SiteManager creates a HotWork permit with isolation unverified, 12-hour duration, crew of 6 | The permit is created | `Reference` = `PWT-0001` (new organization), risk score is computed and persisted (expected `Extreme` under FR-PTW-04 rules), status = `Draft`, audit detail records inputs. |
| AC-PTW-02 | A `Low` risk permit is submitted | The requester submits it | Exactly one `PermitApproval` row exists (Level 1, `SiteManager`, `Pending`); the SiteManager approver is notified. |
| AC-PTW-03 | A `High` risk permit has level 1 approved | Level 2 approver attempts to decide before level 1 | The decision is rejected (`permit.decision.forbidden`); no row changes. |
| AC-PTW-04 | A 3-level permit receives its third `Approved` decision | The final decision is saved in a transaction | Permit status becomes `Approved`, the requester is notified, and all three rows show `Approved` with `DecidedAt`. |
| AC-PTW-05 | A level-2 approver rejects a `Medium` permit with a comment | The decision is saved | Permit status = `Rejected`, remaining pending rows marked `Rejected`/"superseded", requester notified, audit row written. |
| AC-PTW-06 | An `Approved` permit has one unchecked checklist item | A user attempts activation | Activation is blocked with `permit.activate.blocked`; status remains `Approved`; no `ActualStart` is set. |
| AC-PTW-07 | An `Active` permit's `RequestedEnd` was 2 hours ago and the expiry job runs | The job executes | Status becomes `Expired`, `ActualEnd` = `RequestedEnd`, audit + notifications written to requester and SafetyOfficer. |
| AC-PTW-08 | A Worker attempts to approve a level-1 permit decision | The request is processed | Server-side authorization fails, no `PermitApproval` row changes, `Denied` audit entry written. |
| AC-PTW-09 | A `Medium` permit is edited to change Type after level 1 approval | The permit is re-submitted | Risk is rescored, approval rows are regenerated per the new chain, prior decisions are preserved in the audit log only. |
| AC-PTW-10 | An `Active` permit exists and 10 workers are clocked in on that project today | The permit detail loads | The cross-check widget shows attendance count 10 for the permit date/window (FR-PTW-25). |

## 8. Non-functional notes

- **Fail-safe posture:** all safety gates are server-side; the UI may hide unavailable actions but never becomes the enforcement point; scoring failure blocks submission rather than defaulting to `Low` (see FR-PTW-04 fallback).
- **Performance:** permit list p95 <= 400 ms; scoring is an in-process rule evaluation p95 <= 50 ms; expiry job completes for 10,000 permits in <= 60 s.
- **Reliability:** submission, approval generation, decision, and status change each run in a single database transaction; the expiry job is idempotent and safe to re-run.
- **Tenancy:** every permit query is organization-scoped; approval decisions verify both role and organization membership.
- **Usability:** the approval chain is rendered as an ordered stepper showing level, role, decision, and timestamp so an approver can see what precedes them; permits are printable for posting at the worksite.
- **Related NFRs:** authorization matrix, auditability, and background-job requirements are in `docs/requirements/NFRs.md` sections 3, 5, and 6.
