# FieldSuite - Non-Functional Requirements (NFRs)

| Field | Value |
|---|---|
| Document ID | FS-NFR-001 |
| Version | 1.0 |
| Date | 2026-10-06 |
| Applies to | All modules (M0-M5) and the AI/heuristic services |
| Related | PRD (FS-PRD-001), all SRS documents, ADRs |

---

## 1. Scope and quality attribute overview

| # | Quality attribute | Target summary |
|---|---|---|
| QA-1 | Security | Row-level multi-tenancy isolation, Identity cookie auth, server-side authorization, least privilege |
| QA-2 | Data integrity | Relational constraints, server-side computation, transactional multi-step operations |
| QA-3 | Auditability | Every key mutation attributable to user, time, and organization |
| QA-4 | Performance | Server-rendered pages p95 <= 800 ms; lists <= 400 ms |
| QA-5 | Usability | Responsive Bootstrap 5 UI, accessible patterns, printable outputs |
| QA-6 | Availability | >= 99.5% monthly, RTO 4 h, RPO <= 15 min |
| QA-7 | Data protection | Backups, retention, tenant offboarding purge |

## 2. Security

### 2.1 Tenancy isolation (row-level)

| ID | Requirement |
|---|---|
| SEC-01 | Every tenant table (`Project`, `AuditLog`, `Notification`, `Incident`, `CorrectiveAction`, `ToolboxTalk`, `ToolboxAttendance`, `Inspection`, `InspectionItem`, `Snag`, `NCR`, `WorkPermit`, `PermitChecklistItem`, `PermitApproval`, `Estimate`, `EstimateCategory`, `EstimateItem`, `ContractorCompany`, `Worker`, `Attendance`, `OnboardingDocument`, plus `AppUser`) carries a non-nullable `OrganizationId` (or derives it through a parent that does) and is covered by an index whose leading column is `OrganizationId`. |
| SEC-02 | `FieldSuiteDbContext` applies a global query filter `OrganizationId == _currentUser.OrganizationId` on all tenant entities; the filter value comes from the authenticated principal, never from a query-string/route value. |
| SEC-03 | Every insert stamps `OrganizationId` server-side in `SaveChanges` interception; client payloads containing `OrganizationId` are ignored/overwritten. |
| SEC-04 | Any request for an entity that exists but belongs to another organization returns the same not-found response as a nonexistent id (no existence oracle), and writes no audit row for the foreign organization. |
| SEC-05 | Cross-tenant access is covered by automated tests (one per module) that fail the build if isolation breaks. |
| SEC-06 | Child rows created through a parent must verify parent ownership in the same transaction (e.g. creating an `InspectionItem` verifies the `Inspection` is in the caller's organization). |

### 2.2 Authentication (ASP.NET Core Identity, cookie)

| ID | Requirement |
|---|---|
| SEC-07 | Authentication uses ASP.NET Core Identity with the cookie handler: `HttpOnly = true`, `SameSite = Lax`, `Secure = Always` in production, sliding expiration 8 hours, absolute expiration 12 hours. |
| SEC-08 | Password policy: minimum 10 characters, requiring upper, lower, digit, and symbol; lockout after 5 failed attempts for 15 minutes; password reset performed by Admin for the tenant (no public self-service in v1). |
| SEC-09 | All POST/PUT/DELETE endpoints require antiforgery token validation; GET endpoints never mutate state. |
| SEC-10 | Transport is HTTPS only (HSTS in production); HTTP is redirected. |
| SEC-11 | Secrets (LLM API key, DB connection string, Identity signing key) come from environment variables or user-secrets in development; no secrets in source control; configuration is validated at startup with fail-fast on missing production values. |
| SEC-12 | LLM API keys are held server-side only; prompts never include other tenants' data; user content sent to the LLM provider is minimized (see ADR-0005). |
| SEC-13 | File uploads (photos, documents, estimate attachments) are validated by content type and size, stored outside any executable path with server-generated names, and served only through an authenticated, authorized handler that checks tenant ownership. |

### 2.3 Authorization matrix (role x module)

Legend: **F** = full control (create/edit/transition/delete where allowed); **E** = edit and transition; **C** = create/update own records only; **A** = approve/close authority; **V** = read-only; **V(mask)** = read-only with contact PII masked; **V(own)** = own records only; **-** = no access. All cells are enforced server-side.

| Role | Dashboard & KPIs | M1 Safety & EHS | M2 Quality / QC | M3 ePTW | M4 Estimation | M5 Workforce | AI assistant & nudges |
|---|---|---|---|---|---|---|---|
| Admin | V | F | F | F (incl. level-3 approval, suspend, force ops) | F | F | F |
| SiteManager | V | C, E (no final close) | C, E (no close) | C, E, A (level 1; activate/close) | V | C, E (attendance, workers) | V |
| SafetyOfficer | V | E, A (review, resolve, close, assign actions) | V | A (level 2; suspend/resume) | V | V (documents, expiry) | V |
| QCInspector | V | V | F (inspect, retest, close snags, own NCR lifecycle) | V | - | V | V |
| Estimator | V | - | - | V | F (create, price, send, record outcome) | V(mask) | V |
| Worker | V | C (report incident, own toolbox attendance, own actions) | C (raise snag), V (own snags) | V (permits on own projects) | - | V(own) | V |

Additional authorization rules:

| ID | Requirement |
|---|---|
| SEC-14 | Roles are stored on `AppUser.Role` (one of the six defined values) and materialized as a claims principal at sign-in; role changes take effect at next sign-in and are audited. |
| SEC-15 | Admin-only capabilities: force-expire permits, reopen closed records, deactivate workers/contractors, delete draft records, manage users and roles. |
| SEC-16 | Authorization failures return a uniform response (no stack traces), increment a denial counter, and write a `Denied` audit row (Action = attempted action, Detail = reason). |
| SEC-17 | The UI may hide unauthorized actions for usability, but every action is re-authorized in the controller/service layer. |

## 3. Data integrity

| ID | Requirement |
|---|---|
| INT-01 | Foreign keys on all relationships with `Restrict` delete behavior; no cascade deletes from tenant roots (deactivation is preferred over deletion). |
| INT-02 | Money, quantities, rates, percentages, and hours use fixed-point types (`decimal(18,2)` / `decimal(18,3)` / `numeric`) end-to-end; binary floating point is prohibited in totals. |
| INT-03 | Computed values (`LineTotal`, estimate totals, `HoursWorked`, document expiry classification, permit risk level) are computed server-side; client-supplied computed values are ignored. |
| INT-04 | Multi-step operations run in a single transaction: permit submit (score + approval row generation), permit decision (row update + status + notification), inspection completion (items + status), bulk attendance save, reference allocation + insert. |
| INT-05 | Unique constraints: `(OrganizationId, Reference)` on Incident/Inspection/Snag/NCR/WorkPermit/Estimate; `(WorkerId, WorkDate)` on Attendance; `(ToolboxTalkId, WorkerId)` on ToolboxAttendance; `(WorkPermitId, Level)` on PermitApproval. |
| INT-06 | Enum-typed columns are stored as strings (EF conversion) so that schema evolution does not break on numeric reordering; invalid values are rejected at binding. |
| INT-07 | Optimistic concurrency on workflow entities (incident, permit, inspection, snag, NCR, estimate): a transition request carries the expected status; mismatch returns a conflict response showing current state. |
| INT-08 | Database constraints backstop application validation (NOT NULL, CHECK ranges for percentages and hours, FKs); application messages remain the primary UX. |
| INT-09 | Migrations are the single source of schema truth; production schema changes are forward-only, reviewed, and applied by a documented step (no ad-hoc DDL). |

## 4. Auditability

| ID | Requirement |
|---|---|
| AUD-01 | `AuditLog` captures OrganizationId, UserId, Action, EntityType, EntityId, Detail, Timestamp for: create, update, state transitions, approval decisions, discards/deletes, deactivations, attendance overrides, document replace/delete, role changes, and denied attempts. |
| AUD-02 | `Detail` records a compact field-level summary (changed field names and value transition, e.g. `Status: Reported -> Investigating; RiskLevel: Medium -> High`); it must never contain passwords, hashes, or LLM API keys. |
| AUD-03 | Audit rows are append-only: no application path updates or deletes `AuditLog`; audit rows survive entity discard/delete. |
| AUD-04 | Audit writes participate in the same transaction as the mutation they describe (no mutation without its audit row). |
| AUD-05 | Audit views (Admin): filter by date range, user, entity type, action; organization-scoped; retained per section 7. |
| AUD-06 | Safety/permit critical events (incident transitions, permit decisions, activation, expiry) are additionally surfaced in a per-project activity feed readable by SafetyOfficer and SiteManager. |

## 5. Performance targets

| ID | Scenario | Target (p95, server-side) |
|---|---|---|
| PERF-01 | Authenticated page render (any list, 50 rows, 1 tenant) | <= 800 ms |
| PERF-02 | Filtered list query (incidents, snags, permits, estimates, workers, attendance) | <= 400 ms |
| PERF-03 | Dashboard with all six KPI tiles | <= 1.5 s |
| PERF-04 | Estimate detail with 500 items; printable proposal render | <= 600 ms / <= 800 ms |
| PERF-05 | Permit risk scoring (in-process rules) | <= 50 ms |
| PERF-06 | Bulk attendance save (100 workers) | <= 1.5 s |
| PERF-07 | File upload (5 MB photo, 10 MB document) | <= 3 s |
| PERF-08 | Background jobs (permit expiry, estimate expiry, nudge sweep) | complete <= 5 min per run; expiry job for 10,000 permits <= 60 s |
| PERF-09 | Concurrent users per instance (design capacity) | 100 concurrent / 500 requests per minute sustained |

Engineering rules to meet targets: server-side pagination (default 50, max 200), no unbounded `Include` graphs, indexes with leading `OrganizationId`, avoid N+1 (projection to DTOs), and measure with a repeatable test harness before release.

## 6. Usability and accessibility

| ID | Requirement |
|---|---|
| USA-01 | Bootstrap 5 responsive layouts; supported viewports 360 px - 1920 px; primary workflows (report incident, record attendance, approve permit) completeable on a phone without horizontal scrolling. |
| USA-02 | WCAG 2.1 AA baseline: color contrast >= 4.5:1, status conveyed by text + icon (never color alone), all form inputs have associated labels, validation messages adjacent to fields with a summary at top, visible keyboard focus. |
| USA-03 | Consistent chrome: same navigation, reference formatting (`SIN-0001`), status badges, and table patterns across modules; empty states explain the next action. |
| USA-04 | Server-side validation errors are shown inline and preserve user input; destructive actions (discard, deactivate, delete) require explicit confirmation. |
| USA-05 | Printable views (proposal, incident, permit, inspection, day sheet) use print CSS: navigation hidden, page-break control, org name and reference in header. |
| USA-06 | Feedback: every mutation shows an explicit success/failure message; long operations (bulk save, upload) show progress and never double-submit (disabled button + idempotency where applicable). |

## 7. Availability, backup, and retention

### 7.1 Availability

| ID | Requirement |
|---|---|
| AVA-01 | Availability target >= 99.5% per calendar month (excludes agreed maintenance windows); measured from HTTP health checks at 1-minute intervals. |
| AVA-02 | Health endpoint reports database connectivity and migration state; a failing check marks the instance unhealthy for the load balancer/reverse proxy. |
| AVA-03 | Recovery objectives: RTO <= 4 hours, RPO <= 15 minutes. |
| AVA-04 | Application restarts are stateless (session state only in the auth cookie); background jobs are idempotent and safe to re-run after a crash. |

### 7.2 Backup

| ID | Requirement |
|---|---|
| BKP-01 | Nightly full logical backup (`pg_dump` custom format) of PostgreSQL 16 plus continuous WAL archiving to achieve RPO <= 15 minutes. |
| BKP-02 | Backup retention: WAL archived segments 14 days; daily full backups 30 days; monthly full backups 12 months. |
| BKP-03 | Backups are encrypted at rest and stored off-host; restore is rehearsed quarterly into a scratch database with a documented runbook and verification of row counts for key tables. |
| BKP-04 | Uploaded files (photos, documents) are included in the file-level backup consistent with the database backup window. |

### 7.3 Retention and data disposition

| ID | Requirement |
|---|---|
| RET-01 | Operational records (incidents, permits, inspections, snags, NCRs, estimates, attendance, documents) retained for the life of the tenant plus 12 months unless the customer requests earlier deletion. |
| RET-02 | `AuditLog` rows retained for life of tenant plus 7 years (or per customer contractual policy), never deleted by application code. |
| RET-03 | Tenant offboarding: export provided (database extract + files), then purge within 30 days of contract end; audit rows for the purge itself are retained in a vendor-side log. |
| RET-04 | Notifications older than 90 days are pruned (read or unread) by a scheduled job; audit rows are unaffected. |

## 8. Logging, monitoring, and operations

| ID | Requirement |
|---|---|
| OPS-01 | Structured application logging (JSON) with correlation id per request; levels Debug/Info/Warn/Error; no PII beyond identifiers in logs; secrets never logged. |
| OPS-02 | Errors surface a user-safe message with correlation id; full details are logged server-side. |
| OPS-03 | Metrics captured: request duration, error rate, job run outcomes, LLM call success/fallback rate. |
| OPS-04 | Deployment: single-host Kestrel behind a reverse proxy with TLS termination; database migrations run as a release step; rollback via previous build + forward-fix migration policy. |

## 9. Compliance and AI-specific requirements

| ID | Requirement |
|---|---|
| CMP-01 | Role/permission enforcement and audit trails are designed to support ISO 9001 (documented inspections, non-conformance handling) and ISO 45001-style evidence (incident, corrective action, training/toolbox records). |
| CMP-02 | LLM features degrade gracefully: if `ILLMClient` is unconfigured or unavailable, voice/photo drafting and chat fall back to rule-based responses and manual entry forms; no core workflow requires the LLM (see ADR-0005). |
| CMP-03 | LLM interactions are logged (timestamp, feature, token usage, success/fallback) without storing raw sensitive payload content beyond 7 days. |
| CMP-04 | Heuristic services (`IPermitRiskScoring`, `ICostSuggestion`, `INudgeEngine`) are deterministic and unit-tested with fixed input/output tables; their outputs are explainable (rationale stored in audit/nudge detail). |
