# OpenEMR Next

An independent project exploring how an existing healthcare platform, OpenEMR,
can be moved incrementally toward a modular C# architecture without a
big-bang rewrite.

> **Status:** Early implementation. The local stack, the database baseline, and
> the AuthFacade token broker are built and tested. The gateway and domain
> services are not started.
> **Not intended for production clinical use.**

OpenEMR Next is not an official OpenEMR rewrite, fork, replacement, or
affiliated project unless explicitly stated.

## Current scope

This is currently a solo project. Its goal is a runnable, tested, and
documented vertical slice that demonstrates the migration approach. The broader
research vision below is the long-term direction, not the current deliverable.

| Piece | Purpose | Status |
|---|---|---|
| Local stack | OpenEMR, MariaDB, and AuthFacade in one Docker Compose project | Built |
| Database baseline | Reproducible schema and seed data from the OpenEMR installer | Built, not yet cleaned |
| `OpenEmr.Data` | EF Core model scaffolded from the live database | Built |
| AuthFacade | Token broker: password grant to OpenEMR, returns OpenEMR's token response | Built, hardening in progress |
| API gateway (YARP) | Single entry point, token validation at the edge | Planned |
| Patient Management | Registers patients, publishes `PatientRegistered` | Planned |
| Clinical Care | Creates encounters and records vitals for registered patients | Planned |

## What exists today

**Local stack.** A single Compose file at the repository root runs MariaDB,
OpenEMR (pinned to `openemr/openemr:8.0.0.3`), phpMyAdmin, and AuthFacade on one
network. Services address each other by name. The MariaDB host port is 3307.
AuthFacade listens on port 7128.

**Database baseline.** `src/db/maria-db/baseline.sql` holds the schema and seed
data, produced by running the real OpenEMR installer against MariaDB 12.3. A
Dockerfile loads it on first start with an empty volume. A schema-only dump is
kept as a diff target.

**Entity model.** `src/OpenEmr.Data` contains EF Core entities and
`OpenEmrDbContext`, scaffolded from the live database by `scripts/scaffold.ps1`.
Global aliases resolve the `Version` and `Void` name clashes. This model is a
stepping stone: each future service should scaffold only its own tables.

**AuthFacade (`src/Services/AuthFacade`).** A minimal ASP.NET Core API:

- `POST /connect/token` accepts the password grant. It verifies the login with
  OpenEMR's OAuth2 server and returns a token response with `access_token`,
  `token_type`, `expires_in`, and `refresh_token` when OpenEMR issues one. The
  access token is verified before it is returned. A refused login returns 400
  `invalid_grant`. AuthFacade issues no token of its own. See
  [ADR 0013](docs/architecture/0013-pass-openemr-tokens-through.md) and
  [ADR 0016](docs/architecture/0016-facade-token-response-shape.md).
- `GET /me` requires an OpenEMR-issued bearer token. The token is verified
  against OpenEMR's JWKS, and `/me` returns the subject and name.
- `GET /hello` is a liveness check.

## Architecture

```text
            +-------------------+
 client --> | AuthFacade (.NET) |  passes OpenEMR's tokens through
            +---------+---------+
                      | password grant (OAuth2)
            +---------v---------+        +-----------+
            | OpenEMR (legacy)  |<------>| MariaDB   |
            +-------------------+        +-----------+
                                               ^
            seed (one-shot) registers the OAuth client
```

OpenEMR owns the schema, users, password rules, and the OAuth server. AuthFacade
is a token broker in front of it. It does not store passwords or user records.

### Authentication boundary

- **Identity stays in OpenEMR.** Users, ACLs, password hashing, MFA, lockout,
  and expiry are enforced by OpenEMR. AuthFacade delegates to it rather than
  copying its logic.
- **Clients receive OpenEMR's tokens in AuthFacade's own response shape.**
  AuthFacade verifies the access token against OpenEMR's JWKS before returning
  it (ADR 0013), and returns only the four token fields (ADR 0016).
- **Calls to OpenEMR are guarded by circuit breakers.** While OpenEMR is failing,
  logins and token checks fail fast instead of waiting for timeouts (ADR 0017).
- **Minimal client scopes.** The AuthFacade OAuth client holds authentication
  scopes only (`openid`, `profile`, `email`, `offline_access`). The client
  secret does not protect the password grant, because OpenEMR does not check it
  there (ADR 0014). Only the client ID's registration and the scopes limit what
  the client can do.
- **Browser login is not implemented.** The password grant is used because
  AuthFacade is the only client and this is a development stand-in. The
  production target is the authorization-code flow with PKCE, which needs its
  own design for redirects and consent.

### Known gaps

- `/me` confirms identity only. Authorization for OpenEMR data is not built.
- The audience check uses a fixed string rather than a registered client.
- No login rate limiting or lockout in AuthFacade. Brute-force protection relies
  on whatever OpenEMR enforces.
- Login outcomes and `/me` access are not audit-logged.
- The service serves plain HTTP on port 8080, with no HTTPS redirect or HSTS.
- Startup does not wait for OpenEMR, so early requests can fail.
- Form values are not logged, but no test enforces this yet.
- Password plaintext has copies in memory that cannot be zeroed. See
  `docs/architecture/0009-password-plaintext-retrieved-once.md`.
- Login accepts any username and password within the length limits, so the
  complexity policy is not enforced anywhere yet. See
  `docs/architecture/0008-login-credential-rules.md`.
- The OAuth client is provisioned by a seed that inserts into `oauth_clients`.
  The seed updates scope on an existing row but not `grant_types`, so changing
  those requires a manual edit or a fresh volume.
- The password grant is deprecated in OAuth 2.1. Retained for this integration
  only.
- An upstream OpenEMR defect returns 500 on some wrong-password requests, when
  its failed-login counter receives an object where a string is expected.
  AuthFacade cannot prevent this from outside. The strict integration test will
  fail if it recurs.
- The development TLS bypass (`DangerousAcceptAnyServerCertificateValidator`)
  is enabled by `OpenEmr:AcceptAnyServerCertificate`. The setting defaults to
  `false` and is `true` in `appsettings.Development.json`, which the Compose
  stack loads. The API host refuses to start with it set outside Development.
  The Compose stack reaches OpenEMR over plain HTTP inside the Docker network,
  so the bypass has no effect there today. It must be replaced with a trusted
  certificate before any shared use.
- Data-protection keys are not persisted, so the container logs a warning.

## Legacy database compatibility

The legacy database is a transition constraint, not the target architecture.
OpenEMR owns the schema and the OAuth tables. The new components read legacy
data through compatibility adapters. Planned adapters:

- `patient_data`, read by Patient Management
- `form_encounter`, read by Clinical Care

Writes to legacy tables are not part of the slice unless a decision record
approves them. The OAuth client row is written by the seed, which is
environment configuration.

Known problems with the baseline:

- The dump contains development leftovers that must be removed before the
  repository is shared: the `Administrator` user and its hash (`users`,
  `users_secure`), rows in `log`, `api_log`, and `log_comment_encrypt`, session
  rows, and environment-specific `globals` values.
- Every deployment built from this dump shares the same generated keys and
  admin hash. For anything beyond local use, the admin should be created from
  environment variables on first run.
- The `openemr` database account is not in the dump. Compose creates it from
  `.env`.
- Before the dump is shared, confirm it contains no patient rows.

## Repository structure

```text
/
├── docker-compose.yml          # one Compose project: openemr-next
├── .env.example                # placeholders only; .env is gitignored
├── docker/
│   └── open-emr-legacy/        # OpenEMR, MariaDB, phpMyAdmin dev stack
├── src/
│   ├── Services/AuthFacade/    # token broker: Domain, Application, Infrastructure, Api
│   ├── OpenEmr.Data/           # EF Core entities and context (scaffolded)
│   ├── db/
│   │   └── maria-db/           # Dockerfile, baseline.sql, schema.sql
│   └── primitve/               # leftover template, to be deleted
├── tests/
│   ├── Db.Tests/               # database baseline tests (Testcontainers)
│   ├── Services/AuthFacade/    # black-box tests on the live stack
│   └── primitve.tests/         # leftover template, to be deleted
├── tools/
│   └── Scaffold.Host/          # throwaway startup project for dotnet ef
├── scripts/
│   └── scaffold.ps1            # regenerates OpenEmr.Data from the database
└── TestFacade.ps1              # manual login check through the facade
```

Planned additions: `src/Gateway`, `src/PatientManagement`, `src/ClinicalCare`,
and their tests. Each new service gets its own Compose entry, database, and
slim entity model.

## Local development

> Command names below come from the development changelogs. Confirm each one
> against the repository before publishing this README.

**Prerequisites:** Docker Desktop (running), .NET 10 SDK, and PowerShell for the
`.ps1` scripts.

**Configuration.** Copy `.env.example` to `.env` and generate local secrets.
`.env` is gitignored and must never be committed. Application settings live in
one root configuration source; the template holds placeholders only.

**Start the stack:**

```bash
docker compose up -d --build
```

The first start is slow. OpenEMR performs a recursive permission pass over its
bind-mounted tree, which is the main cost on Windows.

**Reset the stack.** Init scripts run only on an empty volume. After editing
`baseline.sql` or changing credentials in `.env`, reset with:

```bash
docker compose down -v
```

This deletes all local data.

**Run the tests:**

```bash
dotnet test tests/Db.Tests                             # starts its own MariaDB
dotnet test tests/Services/AuthFacade/OpenEmrNext.AuthFacade.IntegrationTests  # needs the running stack
```

The integration suite runs a readiness gate first, which checks the database,
the seed, OpenEMR, and AuthFacade under one deadline. It is slow and needs
Docker, so it is not part of a unit-test-only pipeline.

**Check a login by hand:** `./TestFacade.ps1` prompts for the password, logs in
through the facade, and calls `/me`. It prints status codes and property names,
never tokens.

**Regenerate the entity model:** `pwsh scripts/scaffold.ps1`, with `-Tables` for a
subset or `-SkipDocker`. Re-scaffolding overwrites `Entities/` and `Context/`.
Custom code belongs in partial classes elsewhere.

## Evaluation approach

The project will compare the new components against the legacy baseline.
Measurement depth matches the current phase.

**Measured now**

- Functional equivalence for registration and encounter creation, on synthetic
  scenarios (planned with M5)
- Change-impact scope for representative changes
- Architecture test results (planned with M6)
- Security review of the identity bridge (in progress with M1)

**Planned later:** delivery metrics (lead time, deployment frequency, change
failure rate), reliability (availability, p95/p99 latency, recovery time),
scalability, token revocation time, and dual-run discrepancy rates.

Results will report both improvements and trade-offs.

## Research question

> To what extent can an incremental migration of a legacy healthcare platform
> to a modular, service-oriented C# architecture improve maintainability,
> extensibility, scalability, and delivery capability while preserving
> correctness, security, reliability, and clinical continuity?

The current scope answers this for one vertical slice, not for the platform.

## Architectural direction

The target is a modular architecture with selectively deployable services. A
bounded context becomes a separate service only when it gains something
concrete: independent scaling, independent deployment, separate operational
ownership, security isolation, a stable integration boundary, or different
reliability requirements. Otherwise it stays a module or compatibility adapter.

Architecture decisions are recorded as ADRs in `docs/architecture/`. The
decisions recorded in the changelogs are the first candidates.

## Initial bounded contexts

Candidates, not final boundaries: identity and authentication, authorization,
patient administration, clinical records, scheduling, billing and claims,
documents, notifications, reporting, and interoperability. In the current scope,
patient administration and clinical records are under implementation, and
identity is built. The rest are design only.

## Non-goals

This project does not aim to:

- Rewrite all of OpenEMR at once, or convert every context into a service
- Replace the legacy database immediately
- Preserve undocumented behavior without analysis
- Introduce distributed transactions as a default
- Make production clinical claims before validation
- Use identifiable patient data in development or evaluation
- Implement billing, pharmacy, laboratory, decision support, or
  interoperability in the current scope

## Security

This project concerns healthcare software and prioritizes confidentiality,
least privilege, auditability, data integrity, safe failure, and reversible
migration.

- Credentials exposed during development were rotated and replaced with
  generated values. The old values are treated as compromised.
- Secrets live in one gitignored file and are never committed.
- Other development OAuth clients still hold broad `user/*` and `system/*`
  scopes. Remove them from the baseline before the database is shared.
- Report security issues following [SECURITY.md](SECURITY.md).

## Documentation

- [Development Workflow](docs/development-workflow.md)
- [Development and Test Environments](docs/environments.md)
- [Domain-Driven Design Guide](docs/domain-driven-design.md)
- ADRs in `docs/architecture/`
- Changelogs (dated, one per working session)

Links to documents that do not yet exist should be created or removed before
publication.

## License

OpenEMR Next is licensed under the [Apache License 2.0](LICENSE). It is an
independent implementation developed from scratch. It supports compatibility
with selected OpenEMR database structures and workflows but does not copy
OpenEMR source code.

OpenEMR Next is not affiliated with, sponsored by, or endorsed by the OpenEMR
project unless explicitly stated.
