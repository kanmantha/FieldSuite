# FieldSuite - Product Requirements Document (PRD)

| Field | Value |
|---|---|
| Document ID | FS-PRD-001 |
| Product | FieldSuite |
| Version | 1.0 |
| Date | 2026-10-06 |
| Status | Baseline for architecture and build |
| Audience | Product, architecture, engineering, QA |

---

## 1. Vision

FieldSuite is a modular-monolith B2B SaaS that gives small and mid-size construction and manufacturing firms a single system of record for field operations: safety and EHS, quality control, permits to work, cost estimation, and workforce management. It replaces the paper registers, spreadsheets, and WhatsApp threads that today carry a company's compliance evidence, with one auditable, role-aware, per-organization workspace that can be deployed on a single host and operated by non-technical site staff.

## 2. Problem statement

SME contractors (5-200 employees) execute real safety, quality, and scheduling obligations but rarely have dedicated HSE/QC software budgets or IT staff.

1. **Evidence is fragmented.** Incident registers, toolbox talks, inspection checklists, and permits live in paper books and shared drives; producing evidence for a client audit or statutory inspection takes days.
2. **Approvals are informal.** Hot work or confined-space permits are signed on paper, so there is no reliable record of who approved what, at what risk level, or whether the permit lapsed while work continued.
3. **Corrective actions vanish.** Corrective actions from incidents and snags are assigned verbally, never tracked to closure, and re-occur.
4. **Estimation is inconsistent.** Quoted rates come from memory; waste and margin are applied ad hoc, so winning work at the wrong price is common.
5. **Workforce records are incomplete.** Worker documents (IDs, safety certificates, insurance) expire unnoticed; attendance is reconciled manually against active permits and payroll.
6. **Off-the-shelf tools are out of reach.** Enterprise EHS suites are priced per seat with long implementations; spreadsheets do not scale past a handful of projects.

FieldSuite addresses this with a fixed set of opinionated modules, low operational overhead, and a design that works on modest hardware.

## 3. Target market and ICP

### 3.1 Segment

B2B SaaS sold to the business owner or operations manager of a small-to-mid enterprise (SME) in construction and adjacent manufacturing/trades.

### 3.2 Ideal customer profile (ICP)

| Dimension | Description |
|---|---|
| Firm size | 10-200 employees, 1-30 concurrent projects |
| Verticals | General and civil contracting, fit-out and interiors, MEP, scaffolding, steel and fabrication workshops, precast and modular manufacturing, industrial maintenance |
| Roles that buy | Business owner, operations/admin manager, site manager, HSE officer |
| Triggers | Client or principal-contractor requiring digital HSE/QC evidence; a near miss or incident; failed ISO 9001/45001 audit; first large contract with permit-to-work obligations; growth past 2-3 sites |
| Buying behavior | Owner-led, price-sensitive, prefers per-organization subscription, wants setup in days not months, needs printable exports for clients |
| Geography (v1) | English-language, single-currency, one legal entity per tenant |
| Disqualifiers | Requires payroll, accounting, or HRIS integration at go-live; needs offline-first field app; needs per-site legal entity separation; >500 users |

### 3.3 Deployment assumption

One FieldSuite **Organization** equals one tenant (one company). Users, projects, and all operational data are scoped to that organization.

## 4. Personas

### 4.1 Admin / Business Owner

| Aspect | Detail |
|---|---|
| Context | Owner or operations director of a 40-person contracting firm; sets up users, projects, and approval policy; answers to clients and insurers. |
| Goals | 1. See the health of all projects on one dashboard. 2. Enforce that only competent people approve high-risk permits. 3. Keep an unbroken audit trail for clients/auditors. 4. Control cost by quoting from real historical rates. |
| Frustrations | Paper evidence that cannot be produced on demand; staff bypassing approval policy; software that requires a dedicated administrator. |
| Success signal | Can produce a complete incident-to-closure record, a permit approval history, and a proposal PDF in under five minutes. |

### 4.2 Site Manager

| Aspect | Detail |
|---|---|
| Context | Runs day-to-day work on one or more sites; coordinates subcontractors, schedules tasks, and is the first approver for permits. |
| Goals | 1. Issue and approve permits quickly without blocking crews. 2. Know which corrective actions and snags are overdue today. 3. Record attendance without paperwork. 4. Keep the client informed on quality issues. |
| Frustrations | Waiting for signatures; duplicate data entry; not knowing who is on site or what is still open. |
| Success signal | A permit can be raised, approved at their level, and activated in one working session; their site's open items fit on one screen. |

### 4.3 Safety Officer (HSE)

| Aspect | Detail |
|---|---|
| Context | Owns the safety management system: incidents, near misses, toolbox talks, risk assessments, permit safety review. |
| Goals | 1. Capture every incident/near miss with consistent classification. 2. Drive corrective actions to closure before due date. 3. Verify toolbox talk coverage for every crew. 4. Review permit risk ratings and second-level approvals. |
| Frustrations | Under-reported near misses; corrective actions with no owner; attendance sheets that cannot prove who attended. |
| Success signal | Zero overdue corrective actions; every high-risk permit has a documented safety approval; trend data available by project and severity. |

### 4.4 QC Inspector

| Aspect | Detail |
|---|---|
| Context | Performs internal, client, and third-party inspections; raises and verifies snags; manages non-conformance reports. |
| Goals | 1. Run checklists on a tablet/phone on site. 2. Raise snags with location, priority, and photo. 3. Verify retests and close snags with a timestamped record. 4. Raise NCRs with containment actions. |
| Frustrations | Lost checklist sheets; snags closed without retest; no history of who closed what. |
| Success signal | An inspection can be completed, converted to snags, retested, and closed with a complete history. |

### 4.5 Estimator

| Aspect | Detail |
|---|---|
| Context| Prepares quotations for tenders; works across categories (labour, materials, plant, subcontract, preliminaries). |
| Goals | 1. Build estimates from a structured category/item breakdown. 2. Use historical average unit rates instead of guesswork. 3. Apply waste and margin consistently. 4. Produce a client-ready printable proposal. |
| Frustrations | Spreadsheets with broken formulas; forgetting waste; quoting last year's rates; no version history of what was sent. |
| Success signal | A priced, margin-checked proposal with a validity date can be produced and marked sent/accepted/rejected in the system. |

### 4.6 Field Worker

| Aspect | Detail |
|---|---|
| Context | Tradesperson or crew member on site; may be employed directly or through a contractor company; limited system access. |
| Goals | 1. Report a hazard or near miss without fear or friction. 2. Clock in/out and see own attendance. 3. Sign/confirm toolbox talks attended. 4. See own permits and assigned corrective actions/snags. |
| Frustrations | Blaming culture when reporting; not knowing what was assigned to them; paperwork they cannot read on a phone. |
| Success signal | Can report an incident and confirm a toolbox talk from a phone in under two minutes; can see everything assigned to them in one list. |

## 5. Module scope

| ID | Module | Core capabilities | Primary entities | Primary roles | Priority |
|---|---|---|---|---|---|
| M0 | Shared kernel | Tenancy, identity, roles, projects, dashboard KPIs, audit log, notifications, AI assistant | Organization, AppUser, Project, AuditLog, Notification | All | Must |
| M1 | Safety & EHS | Incident/near-miss register with workflow, corrective actions, toolbox talks with attendance | Incident, CorrectiveAction, ToolboxTalk, ToolboxAttendance | SafetyOfficer, SiteManager, Worker | Must |
| M2 | Quality / QC | Inspections with checklists, snag list with retest closure, non-conformance reports | Inspection, InspectionItem, Snag, NCR | QCInspector, SiteManager | Must |
| M3 | ePTW (permits to work) | Permit creation, AI/rule-based risk scoring, tiered approval chain, checklist, activation, expiry | WorkPermit, PermitChecklistItem, PermitApproval | SiteManager, SafetyOfficer, Admin | Must |
| M4 | Estimation | Estimate header/categories/items, waste and margin totals, status workflow, printable proposal | Estimate, EstimateCategory, EstimateItem | Estimator, Admin | Must |
| M5 | Workforce | Contractor companies, workers, attendance, onboarding documents with expiry flags | ContractorCompany, Worker, Attendance, OnboardingDocument | SiteManager, Admin, SafetyOfficer | Must |

### 5.1 Cross-cutting scope

| Capability | Scope |
|---|---|
| Multi-tenancy | Row-level, `OrganizationId` on every tenant table, enforced server-side on every query and command |
| Authorization | ASP.NET Core Identity cookie authentication; six roles: Admin, SiteManager, SafetyOfficer, QCInspector, Estimator, Worker |
| Dashboard | KPI tiles: open incidents, overdue corrective actions, active permits, open snags, expiring documents, today's attendance |
| Auditability | `AuditLog` entries on key mutations (create, update, state transition, approval decision) |
| Notifications | In-app notifications for assignments, approvals, status changes, and nudges (overdue/expiring/aging) |
| AI and heuristics | Rule-based permit risk scoring, cost suggestions, nudge engine, LLM-backed voice/photo-to-draft and chat assistant with graceful fallback |
| Reporting/print | Printable proposal (estimates); printable/list views for incidents, permits, inspections, attendance |
| Cross-module checks | Attendance cross-checked against active permits for the same project and date |

## 6. Out of scope (v1)

1. Accounting, invoicing, payments, and payroll integration.
2. HRIS functions: recruitment, performance review, leave management beyond an `OnLeave` attendance status.
3. Procurement, inventory, plant/asset management, and stock valuation.
4. Project scheduling (Gantt/critical path), BIM, and CAD viewing.
5. Native mobile applications, offline mode, push notifications; v1 is responsive web (Bootstrap 5) in a mobile browser.
6. Email/SMS/push channels; notifications are in-app only in v1 (channel hooks left open).
7. Public REST API, webhooks, and third-party integrations (accounting, ERP).
8. SSO/SAML/OIDC external identity providers, MFA rollout (Identity hooks retained, not configured).
9. Multi-currency, localization, and translation; v1 is English, single currency.
10. Electronic signature, document versioning, redlining, and video capture.
11. Geofencing, GPS tracking, and telematics.
12. Custom report builder and ad-hoc query designer; fixed KPI and list views only.
13. Schema-per-tenant or database-per-tenant isolation models.
14. Retention/disposition workflows for records beyond the retention rules in NFRs.

## 7. Success metrics

| # | Metric | Definition | Target (12 months post-launch) |
|---|---|---|---|
| SM-1 | Activation | New organizations that create at least one project, one user, and one record in three distinct modules within 14 days | >= 70% |
| SM-2 | Time to first incident report | Median elapsed time from organization creation to first incident saved | < 30 minutes |
| SM-3 | Corrective action closure rate | Corrective actions reaching `Done` before `DueDate` | >= 80% |
| SM-4 | Permit compliance | High/Extreme permits with a complete 3-level approval chain before `Active` | 100% |
| SM-5 | Near-miss reporting ratio | Near-miss records as a share of incident records (indicator of reporting culture) | >= 3:1 |
| SM-6 | Snag aging | Open snags older than 30 days as a share of all open snags | < 15% |
| SM-7 | Document currency | Workers with no expired mandatory onboarding document | >= 98% |
| SM-8 | Retention | Monthly active organizations / total organizations | >= 85% |
| SM-9 | Operational reliability | Application availability per calendar month (see NFRs) | >= 99.5% |
| SM-10 | Support load | Support tickets per active organization per month | <= 0.5 |

## 8. Assumptions, dependencies, risks

### 8.1 Assumptions

| ID | Assumption |
|---|---|
| A-01 | One Organization maps to exactly one tenant/company; users do not belong to multiple organizations in v1. |
| A-02 | An Admin user (or the vendor during onboarding) provisions organizations, users, and roles; there is no self-service signup flow in v1. |
| A-03 | Users access FieldSuite through a modern evergreen browser; responsive layouts cover phones and tablets. |
| A-04 | Host has a persistent disk for local file storage (permit/estimate/document uploads) and runs a nightly backup job. |
| A-05 | An OpenAI-compatible LLM endpoint may or may not be configured; the product must remain fully functional without it. |
| A-06 | Reference numbers (`SIN-`, `QIN-`, `SNG-`, `NCR-`, `PWT-`, `EST-`) are sequential per organization and never reused. |
| A-07 | Attendance is recorded by a supervisor on behalf of workers (no self-service time clock hardware). |
| A-08 | Money and quantities use fixed-point decimals in a single currency; no exchange-rate handling. |

### 8.2 Dependencies

| ID | Dependency |
|---|---|
| D-01 | .NET 10 SDK and ASP.NET Core 10 MVC runtime on the deployment host. |
| D-02 | PostgreSQL 16 reachable over the network from the application host. |
| D-03 | EF Core 10 + Npgsql provider; schema managed by migrations. |
| D-04 | Optional: network reachability to a configured OpenAI-compatible endpoint for LLM features. |
| D-05 | Source of truth for role assignment policy is owned by the customer's Admin per tenant. |

### 8.3 Risks

| ID | Risk | Mitigation |
|---|---|---|
| R-01 | Tenant data leakage across organizations | Global query filters, mandatory `OrganizationId` stamping, controller-level scope service, automated cross-tenant tests (see NFRs) |
| R-02 | Permits activated without full approval | Server-side state machine guard; approval rows and checklist are validated in one transaction before `Active` |
| R-03 | LLM outages degrade core workflows | Heuristics-first design; rule-based fallback for risk scoring, nudges, and assistant responses |
| R-04 | Sequential references collide under concurrency | Per-organization counter updated inside the insert transaction with a uniqueness constraint on `(OrganizationId, Reference)` |
| R-05 | SME adoption fails due to data entry burden | Short forms, bulk attendance entry, defaults from templates, printable outputs as immediate value |
| R-06 | Scope creep toward ERP | Enforced by the out-of-scope list and ADR review gate for new cross-module features |
