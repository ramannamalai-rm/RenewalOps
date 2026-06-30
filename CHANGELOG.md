# Changelog

All notable changes to RenewalOps API are tracked here. Phases follow the spec in the original brief.

## [Phase 1] — MVP

### Added
- Solution scaffolding with Clean Architecture boundaries (4 src projects + 2 test projects).
- `docker-compose.yml` orchestrating `api`, `postgres` (16-alpine), and `minio` with healthchecks.
- Multi-stage Dockerfile for the API that installs Tesseract OCR + English language data into the runtime image.
- Domain entities: `User : IdentityUser<Guid>`, `Document`, `AuditEvent`, `ReminderRun` with enums (`UserRole`, `DocumentType`, `DocumentStatus`, `AuditAction`, `ReminderChannel`, `ReminderStatus`).
- EF Core `AppDbContext` extending `IdentityDbContext`, with per-entity `IEntityTypeConfiguration` files, soft-delete query filter on `Document`, and reflection-based `EnumToStringConverter` for all enum properties.
- Initial EF Core migration (`InitialCreate`) generating 10 tables (7 ASP.NET Identity + Documents + AuditEvents + ReminderRuns).
- JWT bearer auth with `JwtTokenService`: register, login, refresh. Role and userId embedded in claims; refresh tokens stored via `UserManager.SetAuthenticationTokenAsync`.
- `MinioStorageService` (Minio v6 SDK) — lazy bucket creation, stream upload/download/delete.
- `TesseractOcrService` — Tesseract for JPG/PNG, regex-driven date extraction for `Expiry Date / Valid Until / Exp. Date / Best Before` patterns. PDF OCR is stubbed (pure Tesseract.NET doesn't handle PDF; full PDF OCR is deferred).
- `DocumentService` orchestrating storage + OCR + persistence + audit; ownership-based access control (Admin sees all, Owner sees own).
- Endpoints under `/api/auth/*` and `/api/documents/*`, all FluentValidation-validated, all swaggered, all (except auth) `[Authorize]`-gated.
- Serilog console logging wired through `UseSerilog()`.
- Swagger UI at `/swagger` with Bearer security scheme.
- Seed data: bootstrap admin user from `Seed:AdminEmail` / `Seed:AdminPassword` config on first startup.
- Unit tests for the OCR expiry-date regex (7 cases).
- Integration tests using `WebApplicationFactory<Program>` with EF Core InMemory + shared `InMemoryDatabaseRoot`, fake storage and OCR services. Covers upload-with-ExpiryDate-and-AuditEvents (Phase 1 acceptance), upload-then-list, soft delete, and unauthenticated-401.

### Notes / decisions
- `AddIdentityCore<User>` instead of `AddIdentity` because the API is JWT-only — `AddIdentity` wires cookie auth as the default scheme, which fights with `AddJwtBearer`.
- Roles are not stored in `AspNetRoles`. Role lives on `User.Role` and is encoded directly into JWT claims. This avoids an extra round-trip to look up roles on every request and keeps the seeding code single-statement. Tables for `AspNetRoles` etc. still exist (Identity creates them) — they're just unused.
- `JwtBearer` validation uses an explicit `KeyId = "renewalops-key"` on the signing key on both ends; .NET 8's `JsonWebTokenHandler` requires `kid` matching for HS256.
- Test factory sets a shared `InMemoryDatabaseRoot` per factory instance so all request scopes within a test class see the same store. Without this, each scope's DbContext gets its own internal InMemory store and uploads "vanish" between requests.

### Acceptance verified
- Upload returns 201; after processing, the document carries an `ExpiryDate`, GET-by-id round-trips it, and the DB contains the expected audit events for the document. (In Phase 1 OCR was synchronous, so the upload response itself carried the `ExpiryDate`; Phase 2 moved OCR to a background job — see below — so the acceptance test now asserts the `ExpiryDate` on the GET and was renamed `Upload_Then_Ocr_Job_Populates_ExpiryDate_And_Writes_AuditEvents`.)

## [Phase 2] — Background jobs + reminder scheduling

### Added
- Hangfire with PostgreSQL-backed storage (`Hangfire.AspNetCore` + `Hangfire.PostgreSql`); `AddBackgroundJobs()` registers the storage and a job server. `/hangfire` dashboard mounted with a custom `IDashboardAuthorizationFilter` (Development: allow loopback; otherwise require the Admin role claim).
- `OcrProcessingJob` — OCR + expiry/issue-date parsing moved off the request thread. Upload now stores the file, persists the document, audits the upload, and enqueues this job; OCR results (and a `DocumentUpdated` audit) are written when it runs.
- `IDocumentJobScheduler` abstraction (Application) so `DocumentService` stays free of Hangfire. Two implementations: `HangfireDocumentJobScheduler` (enqueues / schedules delayed jobs) and `InlineDocumentJobScheduler` (default fallback that runs OCR synchronously in a fresh scope when no job server is present — keeps the app and the DB-free tests working).
- `ReminderScheduler` + `IReminderScheduler` — when an expiry date is detected, creates a Pending `ReminderRun` per configured offset (`Reminders:OffsetsDays`, default T-30d / T-7d / T-1d), skipping offsets already in the past, and dispatches a delayed job for each.
- `ReminderJob` — fires a reminder: marks the `ReminderRun` Sent, logs to console (real email/calendar channels deferred to Phase 4), and writes an audit event. Idempotent (skips already-sent runs).
- `StatusRecomputeJob` — recurring (nightly) job that recomputes document status from expiry: `Expired` if past, `ExpiringSoon` if within `BackgroundJobs:ExpiringSoonWindowDays` (default 30), else `Active`. Skips terminal `Renewed` documents; only changed documents are written, each audited. Registered via `IRecurringJobManager` with `BackgroundJobs:StatusRecomputeCron` (default daily).
- `IReminderRunRepository` + impl; `IDocumentRepository` gains `UpdateRangeAsync` and `GetWithExpiryForRecomputeAsync`.
- Integration tests: reminder scheduling (31-day expiry → 3 reminders at the right offsets; 5-day expiry → only T-1), and status recompute (past/soon/far transitions; `Renewed` left untouched). Suite now at 16 tests (7 unit + 9 integration), all green.

### Notes / decisions
- **Jobs are gated by `BackgroundJobs:Enabled`.** The flag is read directly from the `BackgroundJobs__Enabled` environment variable at startup (not via `builder.Configuration`), because top-level startup code runs before `builder.Build()` and `WebApplicationFactory`'s config overrides only apply post-build. The integration-test host sets this env var so it runs DB-free without a Hangfire server.
- **Inline scheduler is a deliberate fallback, not a test-only hack.** With jobs disabled, OCR still runs (synchronously); delayed reminders become no-ops (the Pending row still records the schedule). This keeps every code path exercisable without a running Postgres + Hangfire.
- **Dashboard auth is an MVP compromise** (loopback in dev / Admin claim otherwise) because the browser-navigated dashboard carries no JWT bearer header. A proper admin login flow is deferred to Phase 4.
- **Reminder scheduling is asserted by the persisted `ReminderRun` rows and their offsets** rather than by advancing a test clock; a swappable `IClock` to fast-forward and assert *firing* is a reasonable Phase 4 refinement.

### Known limitations (verified only under a live stack)
- The actual off-thread Hangfire execution — OCR enqueue/dequeue, delayed reminder firing, and the nightly recurring trigger — only runs against a real Postgres + job server via `docker compose up`. Tests cover all job *logic* directly but not Hangfire's own queue/timer. A full `docker compose up --build` smoke test is still outstanding.
