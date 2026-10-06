# FieldSuite - Architecture Overview

| Field | Value |
|---|---|
| Document ID | FS-ARCH-001 |
| Version | 1.0 |
| Date | 2026-10-06 |
| Scope | Solution structure, layering, tenancy, authentication, module boundaries, AI hook design |
| Related | ADRs (docs/architecture/ADR.md), diagrams (docs/architecture/diagrams.md), NFRs |

---

## 1. Summary

FieldSuite is a **modular monolith**: one deployable ASP.NET Core 10 MVC application with five feature modules plus a shared kernel, backed by one PostgreSQL 16 database, with row-level multi-tenancy, Identity cookie authentication, and pluggable heuristic/AI services. The architecture intentionally optimizes for a small SME product team: one deployment unit, one database, one transaction boundary, with module boundaries enforced by project structure and conventions rather than network hops.

Key decisions and their rationale live in ADRs ([ADR.md](ADR.md)); the structural diagrams live in [diagrams.md](diagrams.md).

## 2. Solution structure

Three production projects plus one test project:

| Project | Kind | Contains | Depends on |
|---|---|---|---|
| `FieldSuite.Domain` | Class library | Entities (`Organization`, `AppUser`, `Project`, `AuditLog`, `Notification`), module entities (Incident, WorkPermit, Estimate, ...), enums, value objects, **service interfaces** (`IPermitRiskScoring`, `ICostSuggestion`, `INudgeEngine`, `ILLMClient`), domain rules and state-transition guards | Nothing (framework-agnostic except `IdentityUser` for `AppUser`) |
| `FieldSuite.Infrastructure` | Class library | `FieldSuiteDbContext`, EF Core configurations, migrations, seed data, repositories/query services, heuristic implementations (rule-based scoring, cost suggestions, nudge engine), `ILLMClient` implementation (OpenAI-compatible HTTP), file storage service, reference-number allocator, background jobs | `FieldSuite.Domain` |
| `FieldSuite.Web` | ASP.NET Core MVC app | Controllers, Razor views, view models, view services, tag helpers, static assets (Bootstrap 5), DI composition root, authentication/authorization setup, middleware (tenant resolution, correlation id), error handling | `FieldSuite.Domain`, `FieldSuite.Infrastructure` |
| `FieldSuite.Tests` | xUnit test project | Unit tests (domain rules, heuristics, validators), integration tests (WebApplicationFactory + test database) including cross-tenant isolation tests | All three (via `InternalsVisibleTo` where needed) |

**Dependency rule:** `Domain` never references `Infrastructure` or `Web`; `Infrastructure` never references `Web`; `Web` is the composition root. All interfaces live in `Domain`; all implementations that need IO live in `Infrastructure`.

## 3. Layering inside the Web request

```
HTTP -> Middleware (correlation id, tenant resolution, antiforgery)
     -> Controller (binds view model, authorizes [Authorize(Roles=...)], validates)
     -> Application/View service (use case orchestration, transactions, audit, notifications)
     -> Domain (entities, state machine guards, computed values, heuristics interfaces)
     -> Infrastructure (DbContext, queries, files, LLM, background jobs)
     -> PostgreSQL
```

Rules:

1. Controllers are thin: bind, authorize, call one service, map to a view model. No `DbContext` in controllers.
2. Services own the unit of work: `SaveChanges` happens once per use case, with the audit row inserted in the same transaction.
3. Domain entities enforce invariants (state transitions, computed totals, validation of ranges); services enforce authorization and tenancy.
4. View models are explicit (never bind entities directly); entity -> view model mapping is done in the service or a mapper.

## 4. Multi-tenancy approach (row-level)

| Concern | Design |
|---|---|
| Tenant identity | `Organization.Id` is the tenant key; `AppUser.OrganizationId` binds a user to exactly one organization. |
| Tenant resolution | After cookie authentication, a `TenantContext` service reads `OrganizationId` from the user's claims (single source of truth). Route/query tenant values are ignored. |
| Enforcement in queries | EF Core global query filter `e => e.OrganizationId == _tenant.OrganizationId` applied to every tenant entity in `FieldSuiteDbContext`. |
| Enforcement on writes | `SaveChanges` interceptor stamps `OrganizationId` on added entities from `TenantContext`; client-supplied values are overwritten. |
| Child ownership | Commands that accept a parent id verify the parent resolves through the filtered `DbSet` (a foreign parent simply does not exist -> not-found). |
| Reference uniqueness | Unique indexes `(OrganizationId, Reference)` per module; allocation happens inside the insert transaction. |
| Residual risks | Raw SQL is prohibited outside `Infrastructure`; any future raw SQL must filter on `OrganizationId` explicitly and is flagged in review. Isolation is proven by automated tests (NFR SEC-05). |

Diagram: container and module diagrams in [diagrams.md](diagrams.md) sections 2 and 3.

## 5. Authentication and authorization

| Aspect | Design |
|---|---|
| Mechanism | ASP.NET Core Identity + cookie authentication (ADR-0003); login/logout/change-password in `Web`. |
| Principal | Claims include `NameIdentifier`, `Name`, `role` (single role claim from `AppUser.Role`), `org_id`. |
| Authorization | `[Authorize(Roles = "...")]` on controllers/actions plus service-layer re-checks for row-level rules (e.g. "only assignee may transition"); policy names mirror the matrix in NFRs section 2.3. |
| UI | Navigation and action buttons rendered conditionally by role/claim; server never trusts the UI (NFR SEC-17). |
| Failures | Uniform authorization failure response + `Denied` audit row. |

## 6. Module boundaries

Each module owns its entities, controllers, views, and view services under a feature folder in `Web` (`Areas/` or folder-per-module: `Safety/`, `Quality/`, `Permits/`, `Estimation/`, `Workforce/`), with entities in `Domain/<Module>` and persistence in `Infrastructure/<Module>`.

| Boundary rule | Enforcement |
|---|---|
| Modules do not query each other's tables directly | Cross-module reads go through a published query service or shared DTO in `Domain` (e.g. `IPermitQuery.ActiveCountForProject(projectId, date)` used by Workforce cross-check). |
| Shared kernel only for cross-cutting concerns | `Organization`, `AppUser`, `Project`, `AuditLog`, `Notification` + shared services (`IAuditService`, `INotificationService`, `IReferenceNumberAllocator`, `ITenantContext`). |
| New cross-module feature | Requires an ADR note (see PRD risk R-06); prefer shared-kernel service over reaching into another module's DbContext queries. |
| State machines | Encapsulated per entity as domain methods (`incident.Submit()`, `permit.Approve(level, user)`) with role/transition guards; enums + explicit transition tables (ADR-0008). |

Cross-module flows that already exist (see diagrams section 5-6):

- Permit activation requires checklist/isolation checks (M3) and is cross-checked against attendance (M5) on the day sheet.
- Snag creation from failed inspection items (M2 internal), and nudges (M1/M2/M5) emitted through `INudgeEngine`.
- Dashboard aggregates read via per-module KPI query services into one dashboard service.

## 7. AI and heuristic hook design

All four capabilities are declared as interfaces in `FieldSuite.Domain` and implemented in `FieldSuite.Infrastructure`, so the core application never depends on a vendor SDK.

| Interface | Implementation (v1) | Consumed by | Fallback |
|---|---|---|---|
| `IPermitRiskScoring` | `RuleBasedPermitRiskScorer` (deterministic type x isolation x duration x crew matrix, per SRS-Permits 4.4) | Permit service on create/edit/submit | Fail-safe: submission blocked with clear error if unavailable; never defaults to `Low` |
| `ICostSuggestion` | `HistoricalAverageCostSuggestion` (avg of org-scoped `EstimateItem.UnitRate` by category+unit; configured defaults otherwise) | Estimate item editor | Returns `Source = None`; UI prompts manual entry |
| `INudgeEngine` | `RuleBasedNudgeEngine` (overdue corrective actions, aging snags, expiring permits/documents, expiring estimates) | Dashboard service + scheduled job + request-triggered sweeps | If it fails, dashboard still renders base KPIs with a warning; nudges retried next sweep |
| `ILLMClient` | `OpenAiCompatibleClient` (config-driven base URL, model, key, timeout; `System.Text.Http`), plus `NullLlmClient` when unconfigured | Voice/photo-to-structured-draft, chat assistant | Rule-based fallback responses; manual entry forms always available (ADR-0005) |

Design rules:

1. **Heuristics-first.** Anything safety- or money-critical (risk level, totals, overdue determination) is deterministic and unit-tested with fixed input/output tables (NFR CMP-04).
2. **LLM is an accelerator, never a gate.** No core workflow requires the LLM; every LLM-assisted action produces a *draft* that a human confirms and that is then validated by ordinary domain rules.
3. **Configuration** in `appsettings.json` + environment (`LLM__BaseUrl`, `LLM__Model`, `LLM__ApiKey`); startup validation warns (does not fail) when unset.
4. **Prompt hygiene:** only the current tenant's minimal context is sent; the client logs success/fallback and token usage, not raw payloads beyond 7 days (NFR CMP-03).
5. **Chat assistant scope:** answers over in-app data through read-only query services (counts, statuses, "what is open today"); when the LLM is unavailable, a rule-based responder answers a fixed set of intents (e.g. "open snags", "active permits", "expiring documents").

## 8. Data access, migrations, and seed

- `FieldSuiteDbContext` (Infrastructure) with `IEntityTypeConfiguration<T>` per entity; string enum conversions; global tenant filter; `SaveChanges` override/interceptor for `OrganizationId` stamping and `AuditLog` insertion hook.
- EF Core migrations checked into source; applied as a release step (NFR INT-09).
- Seed data (`Infrastructure/Seed/`): roles, a demo organization with users per role, sample projects, and reference templates (permit checklist templates per type) so the app is usable immediately after first run.

## 9. Background jobs

A lightweight hosted service (`BackgroundService`) runs an hourly sweep that:

1. Expires permits past `RequestedEnd` (SRS-Permits FR-PTW-17).
2. Expires estimates past `ValidUntil` (SRS-Estimation FR-EST-16).
3. Runs `INudgeEngine` sweeps and de-duplicates notifications (24 h/7 d windows).
4. Prunes notifications older than 90 days (NFR RET-04).

Jobs are idempotent, transactional per record, and log per-item outcomes (NFR AVA-04).

## 10. Cross-cutting services (shared kernel)

| Service | Responsibility |
|---|---|
| `ITenantContext` | Current organization id/name from claims; used by filters and stamping. |
| `IAuditService` | Writes `AuditLog` rows in the caller's transaction with field-level detail. |
| `INotificationService` | Creates in-app notifications with de-duplication keys; renders link targets. |
| `IReferenceNumberAllocator` | Per-org sequential references (`SIN/QIN/SNG/NCR/PWT/EST-NNNN`) allocated inside the insert transaction. |
| `IFileStorage` | Tenant-scoped local storage under `wwwroot/uploads/{orgId}/...` with server-generated names (ADR-0007). |
| `IDashboardService` | Aggregates per-module KPI query services into the dashboard. |

## 11. Testing strategy (FieldSuite.Tests)

| Layer | Scope | Tools |
|---|---|---|
| Unit | Domain state transitions, computed totals, validators, heuristic tables (scoring, cost suggestion, nudge rules) | xUnit, FluentAssertions-style asserts |
| Integration | Controllers/end-to-end via `WebApplicationFactory` with a real PostgreSQL test database; reference allocation under concurrency; transactional audit behavior | xUnit + EF in-memory replaced by Npgsql test DB |
| Isolation | One cross-tenant access test per module asserting not-found + no data leak (NFR SEC-05) | xUnit theory over module cases |
| Regression | Golden cases for risk scoring and estimate totals from SRS acceptance criteria (AC-PTW-01..10, AC-EST-01..09) | xUnit facts mirroring AC tables |

## 12. Configuration surface

| Key | Purpose | Default |
|---|---|---|
| `ConnectionStrings:FieldSuite` | PostgreSQL 16 connection | required |
| `Identity:Password*` | Password policy overrides | policy defaults (NFR SEC-08) |
| `LLM:BaseUrl`, `LLM:Model`, `LLM:ApiKey`, `LLM:TimeoutSeconds` | OpenAI-compatible endpoint | empty => `NullLlmClient` |
| `FileStorage:RootPath` | Upload root | `wwwroot/uploads` |
| `Jobs:SweepIntervalMinutes` | Background sweep interval | 60 |

## 13. Document map

| Document | Content |
|---|---|
| docs/requirements/PRD.md | Vision, ICP, personas, module scope, metrics |
| docs/requirements/SRS-*.md | Functional requirements, data dictionaries, acceptance criteria per module |
| docs/requirements/NFRs.md | Security, authorization matrix, integrity, audit, performance, availability, retention |
| docs/architecture/diagrams.md | C4 context/container, module, ERD, sequence, deployment diagrams |
| docs/architecture/ADR.md | 10 architecture decision records |
