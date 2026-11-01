# 0043. AuthFacade's session state persists in a dedicated MariaDB database

- **Status:** Accepted
- **Date:** 2026-11-01
- **Builds on:** [0026](0026-openiddict-client-drives-the-authorization-code-flow.md), [0027](0027-persist-tokens-in-the-session-cookie.md), [0028](0028-browser-login-and-session-assume-a-single-instance.md)

## Context

ADR 0028 named three independent gaps blocking more than one AuthFacade instance, all rooted in nothing
being shared across processes: (1) OpenIddict's state-token store, a per-process SQLite file; (2)
OpenIddict's own dev-only ephemeral encryption/signing certificates; (3) the `AuthFacadeSession` cookie's
Data Protection key ring, registered with no shared store at all.

Gap 3 is not just a multi-instance problem. `AddDataProtection()` with no persistence configured keeps its
keys in memory or an ephemeral per-container path — confirmed live, by the startup warning "Storing keys
in a directory '/home/app/.aspnet/DataProtection-Keys' that may not be persisted outside of the container."
Every signed-in session (carrying the real OpenEMR access and refresh token, ADR 0027) is silently logged
out on any container restart, today, on a single instance. That is a bigger practical problem than the
narrower ~5-minute login-window risk gap 1 poses, and the one this ADR is actually motivated by.

## Decision

1. **A dedicated MariaDB database and least-privilege user, on the same `mysql` Compose service — not a new
   container.** Named `authfacade` by default (`AUTHFACADE_DB_NAME`/`AUTHFACADE_DB_USER`), created by a new
   one-shot `seed-authfacade-db` service (`scripts/seeds/authfacade-db.sh`), the same `CREATE DATABASE IF NOT
   EXISTS` / `CREATE USER IF NOT EXISTS` / `GRANT` idempotent pattern `apply-credentials.sh` already uses for
   OpenEMR's own database. `auth-facade` depends on it completing, the same way ADR 0037 already made it
   depend on `seed`.
2. **One `DbContext` for everything session-related, nothing else.** `AuthFacadeDbContext` holds both
   OpenIddict's state-token store (`.UseOpenIddict()`) and the Data Protection key ring
   (`IDataProtectionKeyContext`, `PersistKeysToDbContext<AuthFacadeDbContext>()`) — the same database,
   because both exist to support the same session, and splitting them would be two stores with one purpose.
   No identity data, no OpenEMR application data ever lands here.
3. **Deliberately optional as a whole.** `AuthFacadeDbOptions.IsConfigured` is true only when `Host`,
   `Database` and `User` are all set; when it is false, AuthFacade falls back to the same throwaway
   per-process SQLite file this project already used before this ADR. Every unit test and a bare `dotnet
   run` outside Docker take this path — neither has a MariaDB instance to reach, and neither needs the state
   to survive past one in-flight login.
4. **`Microting.EntityFrameworkCore.MySql`** (a Pomelo republish), matching `src/OpenEmr.Data`'s existing
   provider choice rather than introducing a second one for the same database engine.
5. **utf8mb3, not Pomelo's utf8mb4 default, for this context's own tables.** Confirmed live: `utf8mb4`
   (4 bytes/char) pushes OpenIddictTokens' own composite index (`ApplicationId` + `Status` + `Subject` +
   `Type`) past MariaDB's 3072-byte max key length, crashing `EnsureCreatedAsync` with `MySqlException`
   "Specified key was too long." `utf8mb3` (3 bytes/char) fits comfortably and loses nothing these columns
   ever hold — client/application IDs, OAuth subjects, status/type labels, never user-facing free text that
   would need a character outside the Basic Multilingual Plane. Applied via
   `modelBuilder.HasCharSet(CharSet.Utf8Mb3, DelegationModes.ApplyToColumns)`, only when `Database.IsMySql()`
   — the SQLite fallback has no such limit and no such method.
6. **No EF Core migrations.** `EnsureCreatedAsync()` builds the schema from the current model directly, the
   same no-migration-history approach the project already used for the SQLite-only store before this ADR.
   This means `EnsureCreatedAsync` only ever checks whether *any* table already exists in the target
   database/file — not whether the schema is complete — so a schema that crashed partway through (as the
   charset bug above did, on the first attempt) leaves a silently incomplete database behind on every later
   start unless it is dropped and recreated once the underlying cause is fixed. There is no migration to
   repair it forward.

## Consequences

- A container restart no longer logs out an existing session — confirmed live: restarted `auth-facade`,
  queried `DataProtectionKeys` before and after, same single row both times, no new key generated, no "No
  XML encryptor configured" key-regeneration event on the second start.
- Closes gaps 1 and 3 from ADR 0028. Gap 2 — OpenIddict's own dev-only encryption/signing certificates — is
  explicitly **not** addressed here; it needs its own, file-based follow-up mirroring the gateway's TLS cert
  pattern (ADR 0039), tracked separately in ROADMAP.md.
- AuthFacade can now survive a restart or a rolling redeploy without losing session state, but still cannot
  run as more than one concurrent replica until gap 2 closes too — a second instance would still mint
  OpenIddict state tokens it alone can decrypt.
- Verified live end to end: `seed-authfacade-db` created the `authfacade` database and a least-privilege
  `authfacade` user (confirmed via `SHOW GRANTS`, scoped to only that database) separate from OpenEMR's own
  `openemr` database on the same `mysql` service; `auth-facade` started cleanly against it with the full
  OpenIddict + `DataProtectionKeys` schema in place; `/health` and `/oidc/login` (through the gateway) both
  still work.
- All 67 unit tests (up from 60) and the architecture test still pass with no MariaDB present, confirming
  the SQLite fallback is the one every test and a bare `dotnet run` actually exercises.
- Transitive dependencies needed pinning past known advisories: `MySqlConnector` (GHSA-473q-m89c-ghf8, past
  2.5.0), `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` (GHSA-9mv3-2cwr-p262, a cookie-forgery
  bug via improper signature verification — directly relevant, since integrity is exactly what this cookie
  protection feature exists for), and `System.Security.Cryptography.Pkcs` in the unit test project
  specifically (GHSA-2fmv-rv2c-59x7). `System.Security.Cryptography.Xml` is framework-provided for the
  `Sdk.Web` API project (NuGet refuses the pin there, NU1510 "automatically available" — it tracks the
  installed .NET runtime's own patched copy instead) but not for the plain-`Sdk` unit and architecture test
  projects, which both reference the Api project directly and both needed the pin for real;
  `dotnet list package --vulnerable --include-transitive` is clean on all three.

## Alternatives considered

- **Redis for the key ring.** Rejected: OpenIddict has no official Redis store (only EF Core, EF6 and
  MongoDB, since its store abstractions are `IQueryable`-based), so Redis would only ever solve gap 3,
  still leaving gap 1 needing a relational store regardless — strictly more infrastructure than reusing
  MariaDB, not a substitute for it.
- **A second, separate database engine (e.g. Postgres) for isolation.** Rejected: isolation from OpenEMR's
  own data comes from the dedicated database and user, not from the engine. A second engine would be a new
  operational dependency for no functional gain over a second database on the engine already running.
- **Persisted or shared SQLite** (a file on a shared volume instead of a per-process temp path). The
  lightest option on paper, but was not pursued once a dedicated database was chosen: SQLite has no
  real multi-writer story suited to a future second replica (the actual target this whole ADR 0028 chain is
  working toward), where a real database server already does.
- **Fix gap 2 (OpenIddict's certs) in the same pass.** Rejected: the certs and the key ring are independent
  problems with independent fixes (persisted certificate storage vs. a shared store) — bundling them would
  make this change harder to verify in isolation for no shared mechanism between them.

## Related decisions

Closes two of the three gaps [0028](0028-browser-login-and-session-assume-a-single-instance.md) named,
leaving its third (OpenIddict's own dev-only certificates) explicitly open. Builds on
[0026](0026-openiddict-client-drives-the-authorization-code-flow.md)'s state-token store and
[0027](0027-persist-tokens-in-the-session-cookie.md)'s session cookie ticket, the two things now actually
persisted. Follows the same idempotent one-shot-seed-service pattern
[0025](0025-seed-encrypts-the-client-secret.md) and [0037](0037-authfacade-depends-on-seed-completing.md)
already established for OpenEMR's own database and client registration.
