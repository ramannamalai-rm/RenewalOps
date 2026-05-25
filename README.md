# RenewalOps API

A document-lifecycle backend for tracking renewable documents (passports, licenses, insurance, warranties). Ingest a PDF/JPG/PNG, OCR an expiry date out of it, store the original in object storage, and (in later phases) sync to Google Drive/Calendar with reminders before expiry.

> **Status:** Phase 1 (MVP) complete. Phases 2–4 (background jobs, Google sync, hardening) not yet implemented.

---

## Architecture

Clean Architecture with strict inward dependencies:

```
RenewalOps.sln
  src/
    RenewalOps.Api/              ASP.NET Core Web API entrypoint
    RenewalOps.Application/      Use cases, DTOs, validators, service contracts
    RenewalOps.Domain/           Entities, enums, repository contracts
    RenewalOps.Infrastructure/   EF Core, MinIO, Tesseract OCR, JWT, Hangfire (later)
  tests/
    RenewalOps.UnitTests/
    RenewalOps.IntegrationTests/
  docker-compose.yml
```

Dependency direction: `Api → Infrastructure → Application → Domain`. Nothing references upward.

---

## Tech stack

| Concern         | Choice                                                |
| --------------- | ----------------------------------------------------- |
| Runtime         | .NET 8, ASP.NET Core Web API                          |
| Persistence     | PostgreSQL 16 + EF Core 8 (code-first migrations)     |
| Object storage  | MinIO (S3-compatible, runs in Docker)                 |
| OCR             | Tesseract 5 (local, no API calls)                     |
| Auth            | JWT bearer tokens + ASP.NET Core Identity (UserManager) |
| Validation      | FluentValidation                                      |
| Logging         | Serilog (console)                                     |
| API docs        | Swashbuckle / Swagger UI at `/swagger`                |
| Tests           | xUnit + FluentAssertions + EF Core InMemory           |

---

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (with Compose v2)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (only needed for local development outside Docker)

That's it. No global NuGet feed configuration, no Postgres install, no MinIO install — Compose brings the whole stack up.

---

## Quick start

```bash
git clone <this repo>
cd RenewalOps
docker compose up --build
```

Wait for the `api` container to print `RenewalOps API starting on http://[::]:8080`. Then:

| URL                                  | What                                       |
| ------------------------------------ | ------------------------------------------ |
| http://localhost:5000/swagger        | Swagger UI for the API                     |
| http://localhost:9001                | MinIO admin console (`minioadmin` / `minioadmin`) |
| postgres://localhost:5432/renewalops | Postgres (`renewalops` / `renewalops_dev`) |

A seeded admin user is created automatically on first run:

```
Email:    admin@renewalops.local
Password: Admin123!
```

---

## Sample workflow (curl)

### 1. Log in as the seeded admin

```bash
curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@renewalops.local","password":"Admin123!"}'
```

Response:

```json
{
  "accessToken": "eyJhbGciOi...",
  "refreshToken": "ZmFrZS1y...",
  "expiresUtc": "2026-05-12T12:00:00Z"
}
```

Export it for the rest of the session:

```bash
TOKEN="eyJhbGciOi..."
```

### 2. Register a new user (if you don't want to use the admin)

```bash
curl -s -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"alice@example.com","password":"Pass1234!","confirmPassword":"Pass1234!"}'
```

### 3. Upload a document (multipart)

```bash
curl -s -X POST http://localhost:5000/api/documents \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@/path/to/passport.pdf" \
  -F "title=My Passport" \
  -F "documentType=Passport"
```

Returns a `DocumentResponse` with `expiryDate` populated if Tesseract was able to find one in the file.

### 4. List your documents

```bash
curl -s -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/documents?expiringWithinDays=90&search=passport"
```

Supported filters: `type`, `status`, `expiringWithinDays`, `search` (free text on Title + RawExtractedText), `page`, `pageSize`.

### 5. Get a single document

```bash
curl -s -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/documents/{id}
```

### 6. Soft-delete

```bash
curl -X DELETE -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/documents/{id}
```

---

## Authorization model

| Role   | What they can do                                         |
| ------ | -------------------------------------------------------- |
| Admin  | List/get/delete any document; access Hangfire dashboard (Phase 2) |
| Owner  | Upload, list, get, and delete their own documents only   |
| Viewer | Read-only on documents shared with them (sharing model is a Phase 4 stretch goal) |

Roles are stored on the `User.Role` property and embedded as a `role` claim in every JWT. They are **not** wired through ASP.NET Identity's role tables — that adds tables and lookup overhead for a feature we don't need (we never check roles via `[Authorize(Roles=...)]`; ownership checks happen in `DocumentService`).

---

## Local development (without Docker)

```bash
# 1. Start only the dependencies
docker compose up postgres minio -d

# 2. Run the API
dotnet run --project src/RenewalOps.Api
```

The API listens on the URL Kestrel defaults to (typically `https://localhost:5001`). Swagger is at `/swagger`.

### Running tests

```bash
dotnet test
```

11 tests should pass (7 unit + 4 integration). Integration tests use EF Core's InMemory provider and a fake `IStorageService`/`IOcrService`, so they need no external services.

### Migrations

The API runs `await db.Database.MigrateAsync()` on startup, so a fresh `docker compose up` builds the schema automatically.

To create a new migration after changing the model:

```bash
dotnet ef migrations add <Name> \
  --project src/RenewalOps.Infrastructure \
  --startup-project src/RenewalOps.Api \
  --output-dir Persistence/Migrations
```

---

## Configuration

All secrets live in `appsettings.Development.json` (gitignored values like Google client secrets land in `.env`). Production deployments should override via environment variables — the `docker-compose.yml` shows the full set, prefixed with `__` to nest into appsettings (e.g. `Jwt__Secret`).

| Key                              | Default (dev)                 | Description                         |
| -------------------------------- | ----------------------------- | ----------------------------------- |
| `ConnectionStrings:DefaultConnection` | local Postgres            | EF Core connection string           |
| `Jwt:Secret`                     | (dev key in appsettings)      | HS256 signing key (≥32 chars)       |
| `Jwt:Issuer` / `Jwt:Audience`    | `RenewalOps`                  | Standard JWT validation             |
| `Jwt:ExpiryMinutes`              | `60`                          | Access token lifetime               |
| `MinIO:Endpoint`                 | `localhost:9000`              | S3-compatible endpoint              |
| `MinIO:AccessKey` / `MinIO:SecretKey` | `minioadmin`             | MinIO credentials                   |
| `MinIO:BucketName`               | `renewalops`                  | Object bucket (auto-created)        |
| `Ocr:TessdataPath`               | `./tessdata`                  | Path to Tesseract language data     |
| `Seed:AdminEmail` / `Seed:AdminPassword` | `admin@renewalops.local` / `Admin123!` | Bootstrap admin |

---

## Phase status

- ✅ **Phase 1 — MVP**: upload, OCR, list/search, soft delete, JWT auth, audit trail, Docker
- ⏳ **Phase 2 — Background jobs**: Hangfire, async OCR, scheduled reminders
- ⏳ **Phase 3 — Google Drive + Calendar sync**
- ⏳ **Phase 4 — Hardening**: rate limiting, dedup, versioning, OTel, CI

See `CHANGELOG.md` for the per-phase log.
