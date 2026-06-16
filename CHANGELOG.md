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
- `Upload_Document_Should_Return_Created_With_ExpiryDate_And_AuditEvents` proves: upload returns 201, the response carries an `ExpiryDate`, GET-by-id round-trips it, and the DB contains both `DocumentUploaded` and `DocumentViewed` audit events for the document.
