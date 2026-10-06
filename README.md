# FieldSuite

A multi-tenant B2B SaaS platform for construction and manufacturing SMEs, covering five operational modules in one place: **Safety/EHS**, **Quality/Snagging**, **ePTW Work Permits**, **Estimation/BOQ**, and **Light HR/Workforce** — with an AI assistant that works offline by default (rule-based heuristics) and can be pointed at any OpenAI-compatible LLM endpoint.

## Stack

| Layer | Technology |
|---|---|
| Web | ASP.NET Core 10 MVC, Bootstrap 5 |
| Data | EF Core 10, PostgreSQL 16 |
| Auth | ASP.NET Core Identity (cookie), role-based authorization |
| Tests | xUnit (22 tests: services, rules, totals) |
| AI | Pluggable `ILLMClient` — offline rules mode, optional OpenAI-compatible API |

## Modules

- **Safety** — incident lifecycle (Draft → Reported → Investigating → Resolved → Closed), corrective actions, toolbox talks with attendance.
- **Quality** — snag/punch list with photo evidence, QA/QC inspections (checklist → result → pass/fail finalize), NCR containment workflow.
- **Permits (ePTW)** — rule-based risk scoring (type, isolation, gas testing, duration, night work) drives multi-level approvals (SiteManager → SafetyOfficer → Admin), start/complete/close, auto-expiry.
- **Estimation** — categories and line items with waste %, margin, unit-rate suggestions from your own history, status workflow, grand totals.
- **Workforce** — worker profiles, onboarding documents with expiry alerts, contractors, daily attendance board with history.

Cross-cutting: organization scoping (tenancy), audit log, notifications, dashboard with KPIs, nudge engine (overdue actions, expiring permits/documents, aging snags), AI assistant panel (rules + optional LLM).

## Getting started

**Prerequisites:** .NET SDK 10, PostgreSQL 16 running on `localhost:5432`.

1. Verify the connection string in `src/FieldSuite.Web/appsettings.json` (default `Host=localhost;Port=5432;Database=fieldsuite;Username=postgres;Password=postgres`).
2. Run — schema is created and demo data seeded automatically on first start:

```bash
dotnet run --project src/FieldSuite.Web
```

3. Open http://localhost:5000 and sign in with a demo account:

| Role | Email | Password |
|---|---|---|
| Admin | admin@demo.fieldsuite | Demo123! |
| Site Manager | site@demo.fieldsuite | Demo123! |
| Safety Officer | safety@demo.fieldsuite | Demo123! |
| QC Inspector | qc@demo.fieldsuite | Demo123! |
| Estimator | est@demo.fieldsuite | Demo123! |

**Optional AI:** set `Llm:BaseUrl`, `Llm:ApiKey`, `Llm:Model` in `appsettings.json` to any OpenAI-compatible endpoint. Leave empty to stay in offline rules mode.

## Deploying to Render

The repo ships cloud-ready (`Dockerfile`, `render.yaml`, `/health` endpoint).

- The app binds to the `PORT` env var (Render injects it) and prefers `DATABASE_URL` (Render Postgres `postgres://...` URL, auto-converted to an Npgsql connection string with `SslMode=Require`), falling back to `ConnectionStrings:Default`.
- Deploy: connect `github.com/kanmantha/FieldSuite` as a Render Blueprint (uses `render.yaml`) or create a web service manually: runtime `Docker`, build path `.`, plan free/paid, env vars `ASPNETCORE_ENVIRONMENT=Production` and `DATABASE_URL` from your Postgres instance. Migrations + demo data seed automatically on first start.
- Live instance: https://fieldsuite.onrender.com

## Tests

```bash
dotnet test FieldSuite.slnx
```

## Solution layout

```
FieldSuite.slnx
├── src/FieldSuite.Domain          # Entities + enums, no dependencies
├── src/FieldSuite.Infrastructure   # EF Core, Identity seed, services, migrations
├── src/FieldSuite.Web             # MVC controllers, views, view models
├── tests/FieldSuite.Tests         # xUnit service/rule tests
└── docs/
    ├── strategy/                  # Market positioning, pricing, roadmap
    ├── requirements/              # PRD, per-module SRS, NFRs
    └── architecture/              # Architecture doc, diagrams (Mermaid), ADRs
```

## Conventions

- All list/detail queries are scoped by `OrganizationId` (tenancy); children are scoped through their parent project.
- References are generated as `PREFIX-{Id:D4}` (`SIN`, `QIN`, `SNG`, `NCR`, `PWT`, `EST`) after the first save.
- Every POST action requires an anti-forgery token; mutations write audit entries.
- Workflow transitions are validated forward-only in controllers.
