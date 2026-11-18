# OpenEMR Next

An independent project exploring how an existing healthcare platform, OpenEMR,
can be moved incrementally toward a modular C# architecture without a
big-bang rewrite.

> **Status:** Early implementation. The local stack, the database baseline,
> AuthFacade as a BFF (browser login, a revocable session cookie, logout, a
> proxy to OpenEMR's API — rate limited, circuit-broken, and audit-logged,
> over HTTPS, with its own session state persisted in a dedicated database),
> a Blazor WebAssembly dashboard, and a gateway terminating TLS in front of
> all of it are built and tested. The domain services are not started.
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
| AuthFacade | BFF (browser login via OIDC/PKCE, a revocable session cookie, logout, proxies browser calls to OpenEMR's API) — rate limited, circuit-broken, audit-logged, over HTTPS, with its own session state in a dedicated database | Built, hardened ([ADR 0047](docs/architecture/0047-the-identity-bridge-design.md)) |
| WebApp | Blazor WebAssembly: sign-in and identity; patients (list with server-side limit, search, sort; registration; edit; insurance; advance directives) and the clinical screens (encounters, vitals, problems, allergies, immunizations, SOAP and clinical notes, with correct and delete flows). Times are UTC (ADR 0083). 84 component tests | Built, browser-checked Nov 18 |
| API gateway | Terminates TLS, validates the browser's session with AuthFacade on every protected request and forwards the verified user to the services (ADR 0075), and routes by path: AuthFacade's prefixes, `/api` and `/fhir` to Patient Management and Clinical Care, everything else to the WebApp's static files | Built |
| Patient Management | Registers, searches (bounded pages) and reads patients; corrects demographics; insurance; advance directives and documents; the FHIR Patient API (Firely SDK, US Core); an append-only audit trail; `Idempotency-Key` on creating requests (ADR 0084); publishes `PatientRegistered`. Patient merge is still to do; idempotency keys are in progress | Built, merge pending |
| Clinical Care | Encounters, vitals, problems, allergies, immunizations, SOAP and clinical notes, with conditional updates (`ETag` / `If-Match`), permissioned delete, `Idempotency-Key`, an append-only audit trail, and read-only FHIR for five resources; consumes `PatientRegistered` | Built, verification in progress ([design](docs/clinical-care/design/README.md)) |

## What exists today

**Local stack.** A single Compose file at the repository root runs MariaDB,
OpenEMR (pinned to `openemr/openemr:8.0.0.3`), phpMyAdmin, AuthFacade, the
WebApp dashboard, the gateway, and the Login UI on one network. Services
address each other by name. The MariaDB host port is 8320. AuthFacade listens
directly on port 7128 (plain HTTP, for local debugging only) and the Login UI
on 7129, but the gateway — on 7130 (HTTP, redirects only) and 7131 (HTTPS, the
actual entry point, ADR 0039) — is the address everything else actually uses.
The WebApp container itself publishes no host port.

**Database snapshot.** The stack restores its database from a snapshot
(`db.sql`, `sites.tar.gz`, `images.txt`) kept in the git-ignored `snapshots/`
directory and published as a release asset. `scripts/stack/up.sh --restore`
rebuilds the stack from it.

**Entity model.** `src/OpenEmr.Data` contains EF Core entities and
`OpenEmrDbContext`, scaffolded from the live database.
Global aliases resolve the `Version` and `Void` name clashes. This model is a
stepping stone: each future service should scaffold only its own tables.

**AuthFacade (`src/Services/AuthFacade`).** A minimal ASP.NET Core API:

- `GET /api/v1/me` accepts the `AuthFacadeSession` cookie from the browser flow
  below, and returns the subject and name. See
  [ADR 0035](docs/architecture/0035-password-grant-and-bearer-auth-are-removed.md).
- `GET /oidc/login` and `/callback` drive the browser authorization-code flow
  with PKCE against OpenEMR's own OAuth2/OIDC server, via OpenIddict.Client. On
  success the browser only ever receives the `AuthFacadeSession` cookie — never
  a bearer token — which carries the real OpenEMR access/refresh token inside
  its own encrypted, server-side-only ticket. See
  [ADR 0024](docs/architecture/0024-browser-authorization-code-flow-with-pkce.md),
  [ADR 0026](docs/architecture/0026-openiddict-client-drives-the-authorization-code-flow.md), and
  [ADR 0027](docs/architecture/0027-persist-tokens-in-the-session-cookie.md).
- `GET|* /bff/openemr/**` is a reverse proxy (YARP): any path under it is forwarded to OpenEMR
  at the same remaining path, with the session's real OpenEMR access token attached as
  `Authorization: Bearer` server-side. This is how a browser session — which never holds a
  bearer token itself — reaches OpenEMR's API. Requires the `AuthFacadeSession` cookie
  specifically, and is guarded by a circuit breaker and logged to the audit trail (see below). An
  expired access token is silently refreshed first, so the browser never notices
  ([ADR 0045](docs/architecture/0045-silent-refresh-and-admin-session-revocation.md)). See
  [ADR 0029](docs/architecture/0029-yarp-proxies-browser-sessions-to-openemrs-api.md).
  AuthFacade is a BFF (backend-for-frontend): the browser only ever talks to AuthFacade, which does
  the OIDC login, holds the session, and proxies API calls on its behalf.
  **Planned:** once the API gateway (below) and a second backend service exist, this proxy gets one more
  cluster pointed at the gateway — a single hop for anything beyond OpenEMR — rather than learning about
  each new service itself. See [ADR 0030](docs/architecture/0030-bff-proxy-forwards-to-the-gateway-not-each-service.md).
- `GET /logout` ends the session locally, then redirects to OpenEMR's own RP-initiated logout endpoint
  when the session carries an `id_token`, ending both sessions together. See
  [ADR 0040](docs/architecture/0040-rp-initiated-logout.md).
- `POST /sessions/revoke-all` ends every session for the caller's own subject, not just the one calling
  it — the self-service response to a suspected stolen cookie. The `AuthFacadeSession` cookie carries only
  an opaque key; the real ticket lives in a server-side record (same database as ADR 0043) that this
  deletes, so a stolen copy of the cookie stops working on its very next use, with no cooperation from
  whichever browser is holding it. See [ADR 0044](docs/architecture/0044-session-store-backs-the-cookie-for-revocation.md).
- `POST /sessions/revoke/{subject}` — the admin-action counterpart: an OpenEMR subject on a config-driven
  allowlist (`Admin:Subjects`, empty by default) can end any *other* subject's sessions too, logged with
  who did it and against whom. See [ADR 0045](docs/architecture/0045-silent-refresh-and-admin-session-revocation.md).
- `GET /health` is a liveness check.
- `/oidc/login` and `/callback` are rate limited per client address
  ([ADR 0038](docs/architecture/0038-rate-limit-login-and-callback-per-client-address.md)); the token
  exchange, the JWKS fetch, and the BFF proxy's forwarded calls are all guarded by circuit breakers
  ([ADR 0041](docs/architecture/0041-circuit-breakers-guard-the-remaining-openemr-calls.md)); login
  outcomes and BFF proxy access are logged to a dedicated, filterable audit category, with no credentials
  or tokens in it ([ADR 0042](docs/architecture/0042-audit-log-scoped-to-login-and-the-bff-proxy.md)).

**Login UI (`src/Services/LoginUi`).** A static Blazor page with one link to `/oidc/login`, pointed at the
gateway's HTTPS address — the browser reaches AuthFacade through the gateway for this too, not directly,
since the session cookie's `Secure` flag depends on it (ADR 0039).

**WebApp (`src/Services/WebApp`).** A Blazor WebAssembly dashboard, served by its
own nginx container — reached only through the gateway below, never directly:
publishing its own port would let someone load it from the wrong origin and find
the session cookie never attaches. Calls `GET /api/v1/me` through a generated
typed client ([ADR 0031](docs/architecture/0031-nswag-client-generated-from-a-committed-snapshot.md))
and shows the signed-in identity, or a login link if unauthenticated. Patients is
enabled: the list, the patient view with insurance and advance directives, and
registration all call PatientManagement through the same gateway, and the
encounter pages (vitals, problems, allergies, immunizations, SOAP and clinical
notes) call Clinical Care.

**Gateway (`src/Services/Gateway`).** A YARP reverse proxy — the one external
address a browser talks to, for the dashboard and for AuthFacade alike. Terminates TLS with a local dev
certificate, redirects plain HTTP to HTTPS, and applies HSTS on every route
([ADR 0039](docs/architecture/0039-gateway-terminates-tls-authfacade-trusts-forwarded-headers.md)). Routes
AuthFacade's own path prefixes (an `/api` catch-all, `/oidc`, `/callback`, `/logout`, `/sessions`, `/bff`, `/health`,
`/openapi`, `/scalar`) to it, the more specific patient and clinical routes (`/api/v1/patients`, `/api/v1/encounters`,
`/problems`, `/allergies`, `/immunizations`, `/lookups`, and `/fhir/<Resource>`) to Patient Management and Clinical Care, and
everything else to WebApp's static files. Since ADR 0075 it also checks the browser's session with AuthFacade (one
primary-key read per request, failing closed with a 503) before proxying a protected route, and forwards the
verified user, which the services trust only when they also see the gateway's shared secret. See
[ADR 0033](docs/architecture/0033-gateway-fronts-webapp-and-authfacade.md) and
[ADR 0034](docs/architecture/0034-gateway-preserves-the-original-host-header.md)
(preserving the original `Host` header, needed for AuthFacade's own
redirect-URI check to recognize `/callback` when reached through the proxy).

**Patient Management (`src/Services/PatientManagement`).** The first domain service, in hexagonal layers (domain,
read models, ports, application, infrastructure, composition, API) enforced by architecture tests. It writes OpenEMR's
own `patient_data`, `insurance_data` and `documents` rows through a least-privilege database user, so the legacy UI stays
live. It registers patients with duplicate detection, corrects demographics under a content-hash version (`ETag`),
searches in bounded pages, holds insurance and advance directives, serves a FHIR `Patient` API validated against US Core
with the Firely SDK, keeps its own append-only audit trail, and publishes `PatientRegistered` through an outbox. Design set:
[`docs/patient-management/`](docs/patient-management/README.md).

**Clinical Care (`src/Services/ClinicalCare`).** The second domain service, same layering. It writes the legacy
`form_encounter`, `forms`, `form_vitals`, `lists`, `immunizations`, `form_soap` and `form_clinical_notes` rows, again through
a least-privilege user, so legacy's own `EncounterService` and `VitalsService` read what it writes. Every update and delete is
conditional on the version the caller read (`If-Match`: 428, then 412), deletes need legacy's own `admin/super` right (read from its
`gacl_*` tables), creating requests accept an `Idempotency-Key`, and every write lands with an audit row and an outbox event in one
transaction. It consumes `PatientRegistered` and serves read-only FHIR (`Immunization`, `AllergyIntolerance`, `Condition`,
`Encounter`, `Observation`) with `_lastUpdated` search. Design set: [`docs/clinical-care/design/`](docs/clinical-care/design/README.md);
slice-by-slice notes in `docs/clinical-care/`.

## Architecture

A browser talks to one external address for everything, the gateway — the dashboard, and AuthFacade's own
login, callback, logout, and BFF proxy alike. There is no longer a direct-to-AuthFacade path: even the
Login UI's own link now points at the gateway's HTTPS address, since the session cookie's `Secure` flag
depends on it (ADR 0039). The gateway terminates TLS and decides where each request actually goes by path:

```text
 browser --HTTPS--> Gateway --(/api, /oidc, /callback, /logout, /bff, /health)--> AuthFacade (below)
 browser --HTTPS--> Gateway --(everything else)-----------------------------------> WebApp (static dashboard files)
```

Behind the gateway, AuthFacade drives the OAuth2 flow against OpenEMR and keeps its own session-related
state — OpenIddict's state tokens, the Data Protection key ring, and now each session's own ticket — in a
dedicated database, separate from OpenEMR's own:

```text
                        +------------------------+
    browser --HTTPS--> |     Gateway (YARP)     |  terminates TLS, routes by path (ADR 0033, 0039)
                        +-----------+------------+
                                    |
                        +-----------v------------+
                        |   AuthFacade (.NET)    |  BFF: OIDC login, session cookie,
                        |     BFF for browsers   |  proxies browser calls to OpenEMR
                        +------+-------------+---+
                               |             |
           OAuth2 (code, or a  |             |  session state only: OpenIddict's state
           proxied call with   |             |  tokens, the Data Protection key ring, and
           the token attached) |             |  each session's own revocable ticket
                               v             v  (ADR 0043, ADR 0044)
                  +------------------+   +------------------------+
                  | OpenEMR (legacy) |   | mysql: authfacade DB  |
                  +--------+---------+   +------------------------+
                           |
                           v
                  +-------------------+
                  | mysql: openemr DB |
                  +-------------------+
                           ^
           seed (one-shot) registers the OAuth client
```

`authfacade` and `openemr` are two databases, with two separate least-privilege users, on the same `mysql`
Compose service — not two servers. See
[ADR 0043](docs/architecture/0043-authfacade-session-state-in-a-dedicated-mariadb-database.md).

A browser completes `/oidc/login` and only ever receives the `AuthFacadeSession`
cookie — never a bearer token. AuthFacade holds the real OpenEMR token behind
that cookie and attaches it server-side, both for `/me` and for calls proxied
through `/bff/openemr/**`. `GET /logout` ends that session, and OpenEMR's own
session with it when the session carried an `id_token` (ADR 0040).

**Still planned:** the gateway above already exists and already fronts the
dashboard, but AuthFacade's own BFF proxy does not yet forward into it. The
BFF proxy's route table should not grow one entry per new backend service —
instead it should get a second cluster pointed at the gateway, a single hop
for anything beyond OpenEMR — but there is no second backend service
(Patient Management or Clinical Care) yet for that hop to actually reach. See
[ADR 0030](docs/architecture/0030-bff-proxy-forwards-to-the-gateway-not-each-service.md).

```text
 browser --(session cookie)--> AuthFacade (BFF) --(bearer, one hop)--> API Gateway --> Patient Management / Clinical Care
```

OpenEMR owns the schema, users, password rules, and the OAuth server. AuthFacade
is a BFF (backend-for-frontend) for browser sessions — not a general-purpose
gateway. It does not store passwords or user records.

### Authentication boundary

- **Identity stays in OpenEMR.** Users, ACLs, password hashing, MFA, lockout,
  and expiry are enforced by OpenEMR. AuthFacade delegates to it rather than
  copying its logic.
- **Minimal client scopes.** The AuthFacade OAuth client holds authentication
  scopes only (`openid`, `profile`, `email`, `offline_access`). Only the client
  ID's registration and the scopes limit what the client can do.
- **Browser login never hands the browser a bearer token.** The authorization-code
  flow with PKCE (ADR 0024), driven by OpenIddict.Client against OpenEMR's own
  OIDC server (ADR 0026), ends in an `HttpOnly` session cookie only. The real
  OpenEMR access/refresh token lives inside that cookie's encrypted ticket
  (ADR 0027) and is attached to proxied API calls server-side (ADR 0029) — the
  browser itself never sees it. There is no other way to authenticate to
  AuthFacade: the password grant and bearer authentication were removed once
  this flow existed end to end (ADR 0035).
- **Logout ends both sessions, and sits behind the same TLS and rate-limiting as login.** `GET /logout`
  (ADR 0040) signs out the local session, then redirects to OpenEMR's own RP-initiated logout endpoint
  when the session captured an `id_token` — confirming the browser's own cookie is actually gone, not just
  AuthFacade's record of it.
- **A session is a server-side record, not just an encrypted cookie — so it can be revoked.** The
  `AuthFacadeSession` cookie carries only an opaque key (ADR 0044); the real ticket, and the real OpenEMR
  token inside it, lives in AuthFacade's own database (ADR 0043). `POST /sessions/revoke-all` deletes it,
  which is what makes a stolen copy of the cookie stop working everywhere, immediately — not just the
  browser that calls it. A config-driven allowlist of admin subjects can do the same to any *other*
  subject's sessions (ADR 0045).
- **An expiring access token is refreshed silently, server-side.** The BFF proxy redeems the session's
  refresh token before it needs one, with no round trip through the browser; a rejected refresh falls back
  to the stale token and lets OpenEMR's own 401 decide, the same passthrough every other BFF proxy call
  already follows (ADR 0045).

### Known gaps

- `/api/v1/me` confirms identity only. Authorization for OpenEMR data is not built.
- The OAuth client is provisioned by a seed that inserts into `oauth_clients`.
  The seed updates scope on an existing row but not `grant_types`, so changing
  those requires a manual edit or a fresh volume.
- An upstream OpenEMR defect returns 500 on some wrong-password login attempts,
  when its failed-login counter receives an object where a string is expected.
  AuthFacade cannot prevent this from outside. There is currently no automated
  test guarding against a regression: the one that did (a live password-grant
  call) was removed with the password grant itself (ADR 0035).
- AuthFacade's session-related state — OpenIddict's state-token store, the Data Protection key ring, and
  now its own encryption/signing certificates too — all persist past a restart (ADR 0043, ADR 0044, ADR
  0046), closing the three structural reasons a second AuthFacade replica couldn't work. Running more than
  one is still untested; this removes the blockers named against it, it does not itself prove it works.
  See [ADR 0028](docs/architecture/0028-browser-login-and-session-assume-a-single-instance.md).
- The browser login's OIDC scope (`openid profile email offline_access`) does not
  include `api:oemr`, so the BFF proxy's calls to OpenEMR's protected API will be
  rejected by OpenEMR itself until that scope is deliberately widened. See
  [ADR 0029](docs/architecture/0029-yarp-proxies-browser-sessions-to-openemrs-api.md).
- Neither a refresh nor a revocation reaches OpenEMR's own side: it exposes no server-to-server token
  revocation endpoint (confirmed against its own discovery document and source), so the underlying OpenEMR
  refresh token stays valid there until it naturally expires, independent of anything AuthFacade does with
  its own session record.
- Document uploads are capped at 20 MB in the webapp, because the API's request size limit is about 30 MB and
  documents travel as base64 JSON. Raising the limit is an API change.
- **Current gaps (Nov 18):** Patient Management's table-wide `UPDATE` grant on
  `patient_data` is wider than it needs; neither service publishes events for most changes (only registration and Clinical Care's
  writes do); the OpenAPI documents do not declare the `Idempotency-Key` header; the FHIR APIs are read-only apart from `POST /fhir/Patient`,
  and there is no bearer-token path for external FHIR clients; the patient list shows the first 100 patients and the rest by search; the WebApp's
  insurance, directive and immunization-correction screens have not been through the browser check; the gateway's session check has
  never been benchmarked. Patient merge is not built.
- **Legacy quirks that show through, none caused here:** OpenEMR's dashboard can still show deleted vitals as the latest (its query joins on
  `form_id` alone), and it reads "No Known Allergies" for a patient whose only allergy was deleted. See
  [`docs/clinical-care/21-permissions-and-delete.md`](docs/clinical-care/21-permissions-and-delete.md).
- The generated patient client types `GetPatientsAsync`'s name parameter as non-nullable, so the webapp passes
  `null!` for "no filter".
- Times are UTC everywhere (ADR 0083), which is correct while every clock in the stack is UTC (the database, both services and
  legacy's PHP default are). Showing a clinic's local time would need a clinic timezone setting and a conversion rule.

The full list of pending decisions, with options and costs, is in
[`docs/patient-view-pending-decisions.md`](docs/patient-view-pending-decisions.md).

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
├── .github/workflows/ci.yml    # build, restore snapshot, run tests
├── docs/                       # ADRs, changelogs, design notes
├── src/
│   ├── Services/AuthFacade/    # BFF for the dashboard: Composition, Api, Client
│   ├── Services/LoginUi/       # static page linking to AuthFacade's /oidc/login
│   ├── Services/WebApp/        # Blazor WebAssembly dashboard, served by its own nginx container
│   ├── Services/Gateway/       # YARP reverse proxy: the one external origin; checks the session, forwards the verified user
│   ├── Services/PatientManagement/  # Domain, Domain.ReadModels, Application.Ports, Application, Infrastructure, Composition, Api, Client
│   ├── Services/ClinicalCare/       # the same layers for encounters, vitals, problems, allergies, immunizations, notes
│   └── OpenEmr.Data/           # EF Core entities and context (scaffolded)
├── tests/
│   └── Services/               # per service: unit, architecture (reflection-based rules on layer references and on what domain types may expose) and integration (real MariaDB in Testcontainers); WebApp: bUnit component tests
└── scripts/
    ├── setup/new-env.sh        # create .env with generated secrets
    ├── stack/                  # up.sh (--restore), apply-credentials.sh
    ├── snapshot/create.sh      # write a snapshot to snapshots/
    ├── seeds/                  # one seed per service: its database user, grants, and its own tables (outbox, audit, idempotency)
    ├── dev/                    # generate-*-openapi.sh: refresh each committed client snapshot
    └── lib/env.sh              # shared shell helpers (sourced, not run)
```

Each new service gets its own Compose entry, seed script, database user and
tables, and a route added to the existing gateway rather than a new gateway.

## Local development

**Prerequisites:** Docker Desktop (running), .NET 10 SDK, Bash (Git Bash on Windows),
and the GitHub CLI for downloading snapshots.
Each script's requirements are listed in [`scripts/README.md`](scripts/README.md).

**Configuration.** Run `bash scripts/setup/new-env.sh` to create `.env`
from `.env.example` with generated secrets. `.env` is gitignored and must never be
committed. It is the single source for ports, images, and credentials: Compose, the
scripts, CI, and the integration tests all read it, so change a value there and
nowhere else. `scripts/lib/env.sh` holds the shared shell helpers. Every variable is described in
[`docs/configuration.md`](docs/configuration.md).

**Certificates.** Run `bash scripts/setup/new-dev-cert.sh` (the gateway's own TLS certificate, ADR 0039)
and `bash scripts/setup/new-authfacade-certs.sh` (AuthFacade's persisted OpenIddict certificates, ADR
0046) once. Both write to the gitignored `certs/` directory and never overwrite an existing file. The
second one is required: `docker-compose.yml` points `auth-facade` at its output unconditionally, so the
stack fails to start it without having run this first.

**Start the stack:**

```bash
bash scripts/stack/up.sh --restore   # first run: restores the snapshot, applies .env credentials
bash scripts/stack/up.sh             # later runs: resume the stack
```

`--restore` deletes local data. Plain `docker compose up -d --build` also works, but it
installs a fresh OpenEMR instead of restoring the snapshot, which takes much longer.

The first start is slow. OpenEMR performs a recursive permission pass over its
bind-mounted tree, which is the main cost on Windows.

**Reset the stack.** Init scripts run only on an empty volume. After restoring a
new snapshot or changing credentials in `.env`, reset with:

```bash
docker compose down -v
```

This deletes all local data.

**Run the tests:**

```bash
dotnet build OpenEMR-Next.slnx -c Release      # the whole solution: do this before pushing (a service-only build misses broken test fakes)
dotnet test tests/Services/AuthFacade/OpenEmrNext.AuthFacade.IntegrationTests  # needs the running stack
dotnet test tests/Services/ClinicalCare/OpenEmrNext.ClinicalCare.ArchitectureTests  # fast; run it before merging Clinical Care changes
dotnet test tests/Services/PatientManagement/OpenEmrNext.PatientManagement.IntegrationTests  # needs Docker (Testcontainers)
dotnet test tests/Services/WebApp/OpenEmrNext.WebApp.UnitTests   # component tests, no browser, no database
```

Last full results (Nov 18): Patient Management unit 188, architecture 17, integration 156; Clinical Care architecture 69
(unit and integration: 722 and 911 at the last full run, before the M5n to M5r work, which was run in focused sets); WebApp 84;
AuthFacade unit 86; gateway 44 proxy tests and 14 live tests. CI runs the whole solution on every pull request to `main`.

The integration suite runs a readiness gate first, which checks the database,
the seed, OpenEMR, and AuthFacade under one deadline. It is slow and needs
Docker, so it is not part of a unit-test-only pipeline.

## Evaluation approach

The project will compare the new components against the legacy baseline.
Measurement depth matches the current phase.

**Measured now**

- Functional equivalence for registration and encounter creation, on synthetic
  scenarios (**done**: encounter creation compared column by column against legacy, 142 identical and 2 differing by documented rule;
  problems and allergies 215 of 229 identical, the rest explained; the update paths are not yet compared)
- Change-impact scope for representative changes
- Architecture test results (planned with M6)
- Security review of the identity bridge (M1, done — see [ADR 0047](docs/architecture/0047-the-identity-bridge-design.md))

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
identity, patient administration (Patient Management) and clinical records
(Clinical Care) are built as services (M1, M4, M5); the rest are design only.

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
- Service design sets (bounded context, ubiquitous language, context map, domain model, workflows and events, data ownership, UML):
  [Patient Management](docs/patient-management/README.md) and [Clinical Care](docs/clinical-care/design/README.md)
- [Roadmap](ROADMAP.md): milestones, status and what remains before the post-M5 decision gate
- ADRs in `docs/architecture/` — start with [ADR 0047](docs/architecture/0047-the-identity-bridge-design.md)
  for how AuthFacade's design fits together end to end; the rest are the individual decisions behind it
- Changelogs (dated, one per working session)

Reusable guides written out of this project's own experience, generalized so
they apply outside this repository:

- [Handbook: migrating authentication off a legacy system](docs/legacy-auth-migration-handbook.md)
  — the comprehensive one. Everything from the four guides below in a single
  self-contained document: orientation, the decisions, a ten-phase build
  order, hardening/testing/pipeline/container reference, and the production
  gate. Cites nothing; every decision is explained where it is made
- [Migrating legacy authentication](docs/legacy-auth-migration-framework.md) —
  the framework, with every claim cited back to the ADR it came from
- [Migrating authentication behind a legacy system](docs/legacy-auth-facade-framework.md) —
  the same framework restated generically, for a team in a different stack
- [Building an authentication facade, step by step](docs/auth-facade-implementation-guide.md) —
  a sequential build order, each step with a verifiable "done when"
- [Starting a legacy system fast in a test pipeline](docs/fast-legacy-stack-startup.md) —
  how this project's snapshot-and-restore cuts the legacy stack's CI boot from
  twenty minutes to under one
- [Microservice migration playbook](docs/microservice-playbook/README.md) —
  how to extract a domain service from a legacy system with DDD and hexagonal
  architecture: principles and early decisions, discovery, layering, domain
  modeling, legacy-data integration, APIs and events, security, testing,
  operations, a milestone roadmap, checklists, templates, and the lessons
  learned building Patient Management. Framework-neutral; the last document
  maps every rule to this repository

## License

OpenEMR Next is licensed under the [Apache License 2.0](LICENSE). It is an
independent implementation developed from scratch. It supports compatibility
with selected OpenEMR database structures and workflows but does not copy
OpenEMR source code.

OpenEMR Next is not affiliated with, sponsored by, or endorsed by the OpenEMR
project unless explicitly stated.
