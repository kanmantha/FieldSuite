# FieldSuite - Architecture Decision Records

| Field | Value |
|---|---|
| Document ID | FS-ADR-001 |
| Version | 1.0 |
| Date | 2026-10-06 |
| Format | Each record: Context / Decision / Consequences (alternatives noted inside Context) |
| Status vocabulary | Accepted (default for all records below) |

---

## ADR-0001: Modular monolith over microservices

**Status:** Accepted

**Context.** FieldSuite serves SME tenants with a small product team and a single-host deployment budget (NFR AVA-01: single instance, 100 concurrent users). The five modules (Safety, Quality, ePTW, Estimation, Workforce) share identity, tenancy, projects, audit, and notifications. Microservices would introduce distributed transactions across permit/attendance/audit flows, network failure modes, and an operational burden (per-service deploy, tracing, contracts) that the team cannot absorb. Alternatives considered: (a) microservices per module - rejected on operational cost; (b) a single unstructured MVC project ("big ball of mud") - rejected because module boundaries would erode and the domain (SRS documents, ERD) would be unmaintainable.

**Decision.** Build a modular monolith: one ASP.NET Core 10 deployable (`FieldSuite.Web`) plus `FieldSuite.Domain` and `FieldSuite.Infrastructure`, with module boundaries enforced by folder structure, project dependency rules, and cross-module access through shared query/service interfaces rather than direct table access (see docs/architecture/README.md section 6). All writes run in one PostgreSQL transaction.

**Consequences.**
- One deployment unit, one database, one connection pool; RTO/RPO targets are simple to meet.
- Atomic multi-module workflows (permit activation + attendance cross-check + audit row) are trivially correct.
- Module boundaries rely on convention and code review, not the compiler - mitigated by the dependency rule (Domain has no infrastructure references) and review checklist.
- Scaling is vertical first; horizontal scaling later requires session/cookie affinity only (stateless app, see NFR AVA-04).
- Extracting a module into a service later (if ever justified) requires introducing explicit APIs and outbox/consistency handling at that point.

## ADR-0002: Row-level tenancy over schema-per-tenant

**Status:** Accepted

**Context.** Every tenant table carries `OrganizationId` (product spec). Options: (a) row-level security with a shared schema; (b) schema-per-tenant; (c) database-per-tenant. Database-per-tenant multiplies cost and migration effort (hundreds of SME tenants). Schema-per-tenant improves isolation but requires tenant search-path switching, N x migration sets, and complicates cross-tenant analytics and backups. The customer base is SME with modest data volumes and a hard requirement for simple, cheap hosting.

**Decision.** Row-level multi-tenancy in a shared schema: `OrganizationId` on all tenant tables, EF Core global query filter driven by the authenticated principal (`ITenantContext`), server-side stamping on insert, unique references per organization (`(OrganizationId, Reference)`), and not-found responses for foreign rows. Automated cross-tenant isolation tests per module are mandatory (NFR SEC-05).

**Consequences.**
- Single schema, single migration pipeline, single backup; lowest operational cost.
- One database index and one query filter to reason about; tenancy correctness is testable in one place.
- A missing filter is a critical defect; mitigated by: no raw SQL outside Infrastructure, entity configurations centrally registered, and isolation tests in CI.
- Tenant data deletion at offboarding is a filtered purge (NFR RET-03) rather than a DROP, requiring a documented runbook.
- Noisy-neighbor risk on shared resources; acceptable at SME scale with the performance budgets in NFR section 5.

## ADR-0003: ASP.NET Core Identity cookie authentication over external IdP

**Status:** Accepted

**Context.** Buyers are SMEs without an IdP; users are internal staff with a single organization each (PRD A-01, A-02). Options: (a) ASP.NET Core Identity with cookies; (b) external IdP (OIDC/Entra/Google); (c) API tokens/JWT for a future SPA. External IdP adds per-tenant configuration, cost, and support burden; JWTs are unnecessary for a server-rendered app and complicate revocation. The product still needs role-based authorization for six roles and admin-driven user provisioning.

**Decision.** ASP.NET Core Identity with the cookie handler as the sole authentication mechanism; roles stored on `AppUser.Role` materialized as a role claim; admin provisions users per tenant; policy-based authorization in controllers plus service-layer re-checks (NFR section 2.2-2.3). Cookie settings: HttpOnly, SameSite Lax, Secure Always, sliding 8 h / absolute 12 h.

**Consequences.**
- Zero external dependencies for login; works offline from third-party identity outages.
- Login/lockout/password-reset logic is inherited from Identity (password policy per NFR SEC-08).
- No SSO in v1 (PRD out-of-scope item 8); the claim structure leaves room to add an OIDC scheme later without changing authorization.
- Cross-site request forgery handled by antiforgery tokens on all state-changing endpoints (NFR SEC-09).
- Credential storage, hashing, and lockout are Microsoft-maintained rather than hand-rolled.

## ADR-0004: Razor MVC + Bootstrap 5 over SPA

**Status:** Accepted

**Context.** Primary users work from site offices and phones; the workflows are form- and list-heavy (incident report, attendance day sheet, permit approval). Options: (a) server-rendered Razor MVC with Bootstrap 5; (b) SPA (React/Vue/Angular) against a JSON API; (c) hybrid Blazor. An SPA doubles the surface area (API versioning, client state, auth token handling, two validation layers) and risks drift between client and server rules - unacceptable where safety gates must be enforced server-side.

**Decision.** ASP.NET Core 10 MVC with Razor views and Bootstrap 5; progressive enhancement only where valuable (client-side form niceties), with server-side validation as the single authority. Mobile coverage via responsive layouts (NFR USA-01); printable views rendered server-side (proposal, permits, day sheet).

**Consequences.**
- One language, one validation stack, one authorization stack; safety gates cannot be bypassed by a stale client bundle.
- First page render and postback round-trips cost latency; mitigated by p95 budgets (NFR section 5), pagination, and server-side filtering.
- Richer interactions (live dashboards, drag-and-drop estimating) are more expensive than in an SPA; accepted for v1 scope.
- No client-side build toolchain to operate; deployment is a single .NET publish output.
- A future API for mobile apps would be additive (new controllers), not a rewrite of module logic.

## ADR-0005: Heuristics-first with optional LLM over LLM-required design

**Status:** Accepted

**Context.** The product spec requires `IPermitRiskScoring`, `ICostSuggestion`, `INudgeEngine`, and `ILLMClient` (config-driven OpenAI-compatible, graceful fallback). Options: (a) heuristics-first with LLM as an optional accelerator; (b) LLM-first for risk scoring, drafts, and chat. LLM-only would make safety-critical decisions non-deterministic, unauditable, and unavailable offline or when the vendor endpoint fails; it would also make core features unshippable without a paid API key (SME price sensitivity, PRD section 3.2).

**Decision.** Deterministic, unit-tested rule services implement the safety- and money-critical paths (permit risk matrix per SRS-Permits 4.4, historical average rates per SRS-Estimation 4.4, nudge rules per SRS-Workforce FR-WKF-17). `ILLMClient` is config-driven; when unconfigured or failing, a `NullLlmClient` plus rule-based responder provides fallback, and voice/photo-to-draft degrades to manual forms. LLM outputs are always *drafts* validated by ordinary domain rules and confirmed by a human.

**Consequences.**
- Full functionality without an API key or network dependency; LLM is a paid enhancement, not a gate (NFR CMP-02).
- Risk scoring and totals are explainable and auditable (rationale stored in audit detail; NFR CMP-04).
- Cost is predictable; LLM usage is logged with success/fallback rates (NFR OPS-03).
- Two code paths to maintain (rules + fallback responder); mitigated by shared interfaces so consumers do not branch.
- Upgrading to LLM-driven scoring later is a drop-in implementation of `IPermitRiskScoring`, but must not be done without an approval gate because it weakens determinism (revisit via new ADR).

## ADR-0006: PostgreSQL 16 over SQL Server

**Status:** Accepted

**Context.** Options: (a) PostgreSQL 16 with Npgsql; (b) SQL Server. The product targets SME hosting economics; SQL Server licensing cost lands on the customer or vendor and erodes the low-cost positioning. PostgreSQL 16 provides everything required: numeric `decimal` types for money, strong constraint support, WAL archiving for RPO <= 15 minutes, and mature .NET support through Npgsql/EF Core.

**Decision.** PostgreSQL 16 as the system of record, accessed exclusively through EF Core 10 + Npgsql; schema managed by EF migrations; backups via `pg_dump` + WAL archiving (NFR section 7.2). Enum values stored as strings via EF conversion (NFR INT-06).

**Consequences.**
- Zero licensing cost; deployable on commodity Linux hosts and managed Postgres alike.
- `numeric`/`decimal` arithmetic satisfies the money rules (NFR INT-02); no float pitfalls.
- Team must know Postgres specifics (index strategies, `citext`-style case handling done in queries, vacuum implications for large audit tables).
- SQL Server-only features (e.g., certain columnstore patterns) are unavailable - not needed at this scale.
- Portability: staying on EF Core provider APIs keeps a future provider switch theoretically possible, though not planned.

## ADR-0007: Local wwwroot file storage over blob storage

**Status:** Accepted

**Context.** Files in scope are small and low-volume: snag photos (<= 5 MB), onboarding documents (<= 10 MB), estimate attachments. Options: (a) local disk under `wwwroot/uploads`; (b) object/blob storage (S3, Azure Blob, MinIO). Blob storage adds credentials, egress cost, and a second availability dependency for a product that must run on a single host with nightly backups (NFR section 7).

**Decision.** Store files locally under `wwwroot/uploads/{organizationId}/{yyyy}/{MM}/{serverGeneratedName}` via the `IFileStorage` service (shared kernel), served only through an authenticated, authorized handler that checks tenant ownership (NFR SEC-13). Client file names are never used on disk (no path traversal). Files are included in the host-level backup (NFR BKP-04).

**Consequences.**
- No extra vendor, no egress cost, trivial local development; backups cover files with the same runbook as the host.
- Storage scales with one host's disk; acceptable for SME volumes, with a documented migration path: `IFileStorage` is an interface, so a blob implementation can be swapped in without touching controllers (revisit when a tenant needs > 100 GB or multi-instance hosting).
- Serving through a handler (not static anonymous files) costs a controller action per download; acceptable for the volumes involved.
- Local disk failure risk is mitigated by the off-host backup requirement, not eliminated by design - an explicit operational obligation.

## ADR-0008: Enum-based state machines with explicit transition tables over a state library

**Status:** Accepted

**Context.** Six entities carry workflows (Incident, CorrectiveAction, Inspection, Snag, NCR, WorkPermit, Estimate). Options: (a) status enums plus domain methods with transition tables; (b) a workflow/state-machine library (e.g., Stateless, Workflow Core). The transitions are small, fully specified in the SRS documents, and coupled to role permissions and side effects (notifications, audit, `ReviewedById`/`ClosedAt` stamps).

**Decision.** Statuses are string-converted enums in the domain model (NFR INT-06); each entity exposes intent-named methods (`Submit()`, `Approve(level, user)`, `Close(user)`) that validate the transition against a declarative transition table (role set + preconditions) and throw a typed domain exception on violation. All side effects (audit, notifications, stamps) happen in the calling service within one transaction.

**Consequences.**
- No additional dependency; the transition tables in the SRS documents map 1:1 to code and to tests.
- Persistence is simple - a status column; no state-machine runtime tables or serialization format.
- Optimistic concurrency checks expected-status on every transition (NFR INT-07) are implemented once in the guard.
- Visual workflow editing is not possible; acceptable because workflows are fixed by compliance practice and changing them is a code change behind a migration-free enum string.
- Complex long-running compensation (sagas) is out of scope for a monolith with one database.

## ADR-0009: Server-side decimal arithmetic for all totals over client-side calculation

**Status:** Accepted

**Context.** Estimates, permit durations, attendance hours, and waste/margin percentages drive money and compliance outcomes. Options: (a) server-computed `decimal` with client display only; (b) client-side calculation with server spot-checks; (c) floating-point types for speed. Client-trusted totals are a correctness and integrity risk (tampering, rounding drift); floats introduce representation errors in money (PRD A-08).

**Decision.** All computed values are produced server-side using `decimal` end-to-end (C# `decimal`, PostgreSQL `numeric`, 2-dp half-up rounding at line and aggregate level); client-posted computed values are ignored (NFR INT-02, INT-03). Estimates display a server-rendered totals block; the printable proposal renders the same server values (SRS-Estimation FR-EST-11, FR-EST-13).

**Consequences.**
- Totals always agree between list, detail, and printable output; audit can reproduce any historical proposal from stored rows.
- Slight overhead versus float is irrelevant at these row counts.
- Client interactivity (instant recalculation while typing) requires a round trip or a mirrored JS implementation; v1 accepts the round trip (form posts), which keeps a single source of truth.
- Database CHECK constraints backstop range rules (percentages 0-100, hours 0-24) as defense in depth.

## ADR-0010: Per-organization sequential references over GUIDs or global sequences

**Status:** Accepted

**Context.** Humans read and quote incident/permit numbers in the field: `SIN-0001`, `PWT-0042`, `EST-0007`. Options: (a) per-organization counters; (b) GUID/ULID primary keys surfaced to users; (c) a single global sequence per prefix. GUIDs are unreadable and unquotable; global sequences leak other tenants' volume and complicate tenant export/restore.

**Decision.** Human-readable references are generated per organization and prefix (`SIN`, `QIN`, `SNG`, `NCR`, `PWT`, `EST`) by `IReferenceNumberAllocator`: the next value is read and incremented inside the insert transaction, and a unique constraint on `(OrganizationId, Reference)` is the final arbiter under concurrency (product spec assumption A-06; NFR INT-05). References are immutable and never reused, even after discard (the audit row retains them).

**Consequences.**
- Field staff get short, quotable identifiers; exports and printed permits are self-describing.
- Contention on the per-org counter row is bounded because creates are low-frequency relative to traffic; a retry loop handles rare collisions.
- Tenant restores keep numbering consistent because counters live in the same database (restore integrity requirement).
- Number gaps can occur when a transaction rolls back (counter is not rolled back) - accepted; gaps are preferable to reuse and are documented as expected behavior.
- Requires the counter table/row to be tenant-scoped and included in the same backup as the data (no external state).
