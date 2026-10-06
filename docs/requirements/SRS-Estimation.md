# SRS - Module 4: Estimation

| Field | Value |
|---|---|
| Document ID | FS-SRS-EST |
| Version | 1.0 |
| Date | 2026-10-06 |
| Scope | Estimates, categories, line items, totals, workflow, printable proposal |
| Parent document | PRD (FS-PRD-001), section 5 (M4) |

---

## 1. Purpose

This specification defines the functional and data requirements for the Estimation module: structured estimates built from categories and line items, server-computed waste and margin totals, historical rate suggestions (`ICostSuggestion`), a draft-to-accepted workflow, and a client-ready printable proposal (PRD section 4.5).

## 2. Actors and roles

| Role | Module capability summary |
|---|---|
| Admin | Full control: create/edit/delete any estimate, override status at any point, set margin, mark accepted/rejected, view all estimates. |
| Estimator | Primary actor: create and edit estimates, manage categories/items, use cost suggestions, submit (`Sent`), record client outcomes, generate the printable proposal. |
| SiteManager | Read-only on estimates (visibility of scoped project work); may add internal `Notes` on estimates for projects they manage. |
| SafetyOfficer | No access to the Estimation module (list and detail hidden; server-side denied). |
| QCInspector | No access to the Estimation module (server-side denied). |
| Worker | No access to the Estimation module (server-side denied). |

## 3. Functional requirements

### 3.1 Estimate header and references

| ID | Requirement |
|---|---|
| FR-EST-01 | **Create estimate.** Estimator and Admin shall be able to create an estimate with Title, ClientName, optional ProjectId, ValidUntil, Notes, MarginPercent; `CreatedById` = caller; `CreatedAt` = server time; status starts `Draft`; `Reference` = `EST-NNNN` per FR-EST-02. |
| FR-EST-02 | **Automatic reference.** `Reference` shall be `EST-` + 4-digit per-organization sequence (`EST-0001` first), allocated in the insert transaction, unique with `OrganizationId`, immutable. |
| FR-EST-03 | **Edit estimate header.** Estimator and Admin may edit header fields while status is `Draft` or `Sent`; after `Accepted`/`Rejected`/`Expired` the header is read-only except `Notes` (Admin), each edit audited. |
| FR-EST-04 | **Delete estimate.** Only `Draft` estimates may be deleted, by the creator or Admin; deletion writes an `AuditLog` entry (action `Discard`) containing the reference and title. Estimates with any other status cannot be deleted. |

### 3.2 Categories and items

| ID | Requirement |
|---|---|
| FR-EST-05 | **Manage categories.** Authorized users shall be able to add, rename, and remove `EstimateCategory` rows (e.g. Labour, Materials, Plant, Subcontract, Preliminaries); each has `Name` and `SortOrder`; removal is blocked when the category contains items (items must be moved or deleted first). |
| FR-EST-06 | **Reorder categories.** Authorized users shall be able to change `SortOrder` (sequential 1..n after each save) so the printable proposal lists categories in the chosen order. |
| FR-EST-07 | **Manage items.** Authorized users shall be able to add, edit, and delete `EstimateItem` rows within a category with Description, Quantity, Unit, UnitRate, WastePercent. |
| FR-EST-08 | **Line total computation.** `LineTotal` shall be computed server-side as `Quantity * UnitRate * (1 + WastePercent / 100)`, rounded to 2 decimal places using decimal arithmetic; client-supplied totals are never trusted or persisted. |
| FR-EST-09 | **Cost suggestion.** On adding an item (or on explicit "suggest rate" action), the system shall call `ICostSuggestion.Suggest(organizationId, categoryName, unit, description)` which returns the historical average `UnitRate` for matching category+unit within the organization (from previously saved items), plus a sample count; when history is insufficient (fewer than 3 matches) the service returns configured default rates or no suggestion, clearly labelled as such in the UI. |
| FR-EST-10 | **Suggestion application.** A suggestion is applied only on user confirmation; the applied rate and whether it came from history or defaults is recorded in `AuditLog.Detail` when the item is saved. |

### 3.3 Totals and margin

| ID | Requirement |
|---|---|
| FR-EST-11 | **Totals computation.** The system shall compute and display, using decimal arithmetic at 2-dp rounding: **Line waste** per item = `Quantity * UnitRate * WastePercent / 100`; **Subtotal** = sum of all item `LineTotal` (waste included per line); **Waste total** = sum of per-item waste; **Net before margin** = sum of `Quantity * UnitRate`; **Margin amount** = `Subtotal * MarginPercent / 100`; **Grand total** = `Subtotal + Margin amount`. |
| FR-EST-12 | **Margin guardrail.** `MarginPercent` outside 0-100 is rejected; values above 50 trigger a non-blocking warning that must be dismissed before saving (recorded in audit detail). |
| FR-EST-13 | **Totals recalculation.** Totals are recalculated server-side on every item/category change and on every GET of the estimate detail (no cached or client-stored totals); a mismatch between client-displayed and server totals forces a refresh with a notice. |

### 3.4 Workflow and outputs

| ID | Requirement |
|---|---|
| FR-EST-14 | **State machine.** Allowed transitions: `Draft -> Sent`, `Sent -> Draft` (revise), `Sent -> Accepted`, `Sent -> Rejected`, `Draft -> Expired`, `Sent -> Expired`. `Accepted`, `Rejected`, and `Expired` are terminal. Only the creator (Estimator), Admin, or (for `Accepted`/`Rejected`) Admin recording a client outcome may transition. |
| FR-EST-15 | **Send precondition.** `Draft -> Sent` requires: at least one category, at least one item in at least one category, all items valid (section 6.1), `ValidUntil` >= today, and a non-empty `ClientName`. On send, the system notifies Admin and marks the estimate as sent (audit `Send`). |
| FR-EST-16 | **Automatic expiry.** A background job (hourly) shall set estimates in `Draft` or `Sent` with `ValidUntil` < today to `Expired`, write an audit entry, and notify the creator and Admin. |
| FR-EST-17 | **Printable proposal.** An authorized user shall be able to open a print-friendly proposal view (Razor view with print CSS) containing: organization name, `Reference`, Title, ClientName, optional project, validity date, category/item breakdown with quantities, units, rates, line totals, waste total, subtotal, margin amount, grand total, `Notes`, and a signature/acceptance block. The view is read-only, printable to PDF via the browser, and organization-scoped. |
| FR-EST-18 | **Notifications.** Create in-app notifications for: estimate sent (Admin), accepted/rejected (creator), expiring within 7 days (creator), expired (creator and Admin). |
| FR-EST-19 | **Audit log.** Create, header/category/item changes, status transitions, and suggestion applications write `AuditLog` rows with field summaries. |
| FR-EST-20 | **Permissions enforcement.** Section 2 role rules enforced server-side; denied attempts return the standard authorization error and a `Denied` audit entry. |

## 4. Data dictionary

### 4.1 Estimate

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| OrganizationId | int (FK -> Organization) | auto | Stamped server-side; indexed. |
| ProjectId | int? (FK -> Project) | no | Optional scope; when present must be in caller's organization and active. |
| Reference | string(16) | auto | `EST-NNNN` per organization; immutable; unique with `OrganizationId`. |
| Title | string(200) | yes | Non-empty, max 200 characters. |
| ClientName | string(200) | yes | Non-empty; required again before `Sent`. |
| Status | enum | auto | `Draft`, `Sent`, `Accepted`, `Rejected`, `Expired`; default `Draft`. |
| ValidUntil | date | yes | >= `CreatedAt` date at creation; >= today for `Draft -> Sent`. |
| Notes | string(4000) | no | Max 4000; appears on printable proposal. |
| CreatedById | string (FK -> AppUser) | auto | Caller at creation; immutable. |
| CreatedAt | datetime | auto | Server time. |
| MarginPercent | decimal(5,2) | yes | Range 0-100 inclusive; > 50 triggers warning (FR-EST-12). |

### 4.2 EstimateCategory

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| EstimateId | int (FK -> Estimate) | yes | Parent estimate in same organization. |
| Name | string(100) | yes | Non-empty, max 100; duplicates allowed but discouraged by warning. |
| SortOrder | int | yes | Sequential within estimate after save (1..n); default = append at end. |

### 4.3 EstimateItem

| Field | Type | Required | Rules / validation |
|---|---|---|---|
| Id | int (PK) | auto | Server generated. |
| EstimateCategoryId | int (FK -> EstimateCategory) | yes | Parent category in same organization/estimate. |
| Description | string(300) | yes | Non-empty, max 300 characters. |
| Quantity | decimal(18,3) | yes | > 0; max 999999.999. |
| Unit | string(20) | yes | Non-empty (e.g. `m`, `m2`, `m3`, `kg`, `day`, `item`, `hr`). |
| UnitRate | decimal(18,2) | yes | >= 0; max 99999999.99. |
| WastePercent | decimal(5,2) | yes | 0-100 inclusive; default 0. |
| LineTotal | decimal(18,2) | computed | `Quantity * UnitRate * (1 + WastePercent / 100)`, 2-dp, server-computed (FR-EST-08); not client-writable. |

### 4.4 Cost suggestion contract (ICostSuggestion)

| Element | Definition |
|---|---|
| Input | `organizationId`, `categoryName`, `unit`, optional `description`. |
| Output | Suggested `UnitRate` (decimal), `SampleCount` (int), `Source` (`Historical` / `ConfiguredDefault` / `None`). |
| History basis | Average of `UnitRate` across all `EstimateItem` rows in the organization joined to their category by case-insensitive name and matching `Unit`, excluding items from estimates in `Rejected`/`Expired` status when the sample count allows. |
| Threshold | `SampleCount >= 3` for `Historical`; otherwise fall back to configured defaults; if none configured, `Source = None` and the UI prompts manual entry. |

## 5. Workflow state machine (Estimate)

| From | To | Allowed roles | Preconditions / side effects |
|---|---|---|---|
| Draft | Sent | Estimator (creator), Admin | FR-EST-15 preconditions; audit `Send`; notify Admin. |
| Sent | Draft | Estimator (creator), Admin | Revise; audit `Revise`; structure unlocked for editing. |
| Sent | Accepted | Admin, Estimator (creator) | Records client acceptance; audit `Accept`; notify creator. |
| Sent | Rejected | Admin, Estimator (creator) | Records client rejection; audit `Reject`; notify creator. |
| Draft | Expired | System (hourly job) | `ValidUntil` < today; audit `Expire`. |
| Sent | Expired | System (hourly job) | `ValidUntil` < today; audit `Expire`; notify creator and Admin. |
| Accepted / Rejected / Expired | (terminal) | - | Only clone-to-new-draft by Estimator/Admin (new reference) is permitted. |

## 6. Validation rules (consolidated)

| ID | Rule | Message key |
|---|---|---|
| V-EST-01 | `Title` and `ClientName` non-empty, <= 200 chars. | `estimate.required` |
| V-EST-02 | `ValidUntil` >= `CreatedAt` date; >= today for sending. | `estimate.validity.past` |
| V-EST-03 | `Quantity` > 0; `UnitRate` >= 0; `WastePercent` 0-100; `MarginPercent` 0-100. | `estimate.range.invalid` |
| V-EST-04 | `Unit` non-empty on every item. | `estimate.unit.required` |
| V-EST-05 | `Draft -> Sent` requires >= 1 category and >= 1 item (FR-EST-15). | `estimate.empty` |
| V-EST-06 | Category deletion blocked while it contains items. | `estimate.category.notempty` |
| V-EST-07 | `ProjectId`, when set, resolves inside caller's organization; otherwise not-found with no existence leak. | `project.notfound` |
| V-EST-08 | Status transitions limited to the matrix in section 5. | `estimate.transition.invalid` |

## 7. Acceptance criteria

| ID | Given | When | Then |
|---|---|---|---|
| AC-EST-01 | A new organization's Estimator creates an estimate | The record saves | `Reference` = `EST-0001`, status = `Draft`, `CreatedById` = caller, `OrganizationId` stamped server-side. |
| AC-EST-02 | An item has Quantity 10, UnitRate 125.50, WastePercent 7.5 | The item is saved | Server-computed `LineTotal` = 10 * 125.50 * 1.075 = 1349.13 (rounded 2-dp); a client-posted total of any other value is ignored. |
| AC-EST-03 | The estimate subtotal is 10,000.00 and `MarginPercent` is 18 | The detail view loads | Waste total, subtotal, margin amount 1,800.00 and grand total 11,800.00 are all displayed consistently on screen and in the printable proposal. |
| AC-EST-04 | An item category "Materials" with unit `kg` has 5 historical rates averaging 42.75 in the organization | The estimator requests a rate suggestion | `ICostSuggestion` returns 42.75 with `Source = Historical` and `SampleCount = 5`; applying it writes an audit detail containing the suggested rate. |
| AC-EST-05 | An estimate with zero items | The estimator attempts `Draft -> Sent` | The transition is rejected with `estimate.empty`; status remains `Draft`. |
| AC-EST-06 | A `Sent` estimate has `ValidUntil` = yesterday and the expiry job runs | The job executes | Status becomes `Expired`, audit row written, creator and Admin notified once. |
| AC-EST-07 | A Worker requests the estimate list or detail | The request is processed | Server-side authorization fails with no data returned, and a `Denied` audit entry is written. |
| AC-EST-08 | The printable proposal is opened for an accepted estimate | The view renders | It shows reference, client, validity, category breakdown in `SortOrder`, all totals from FR-EST-11, notes, and a signature block; the view contains no edit controls. |
| AC-EST-09 | An estimate in `Accepted` status is edited | The estimator submits a change | Header and items are read-only (except `Notes` by Admin); response explains the restriction; audit shows no mutation. |

## 8. Non-functional notes

- **Arithmetic integrity:** all money paths use `decimal` end-to-end (C#, PostgreSQL `numeric`); no floating point in totals; rounding is half-up to 2 dp at line level and aggregate level.
- **Performance:** estimate list p95 <= 400 ms; detail with 500 items p95 <= 600 ms; proposal render p95 <= 800 ms.
- **Tenancy:** estimate queries are organization-scoped; cost suggestions only aggregate within the caller's organization (no cross-tenant rate leakage).
- **Reproducibility:** because totals are derived, the stored item rows are the source of truth; any historical proposal can be reproduced by re-rendering the same rows.
- **Usability:** category grouping with inline totals, sticky summary bar on desktop, single-column item entry on mobile; printable proposal uses print CSS with page-break control.
- **Related NFRs:** authorization matrix, auditability, and performance targets are in `docs/requirements/NFRs.md` sections 3, 5, and 6.
