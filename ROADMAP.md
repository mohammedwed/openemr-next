# OpenEMR Next Roadmap

This roadmap has two layers:

1. **Current scope:** the milestones for the portfolio vertical slice. Status
   comes from the dated changelogs, which are the source of truth.
2. **Long-term phases:** the broader migration vision. These are directional and
   will be revisited once the slice produces evidence.

The roadmap is evidence-driven. A capability becomes an independently deployable
service only when its boundary, operational model, and benefits are understood.
Dates are not promises.

## Guiding principles

- Preserve clinical continuity and data integrity.
- Prefer incremental migration over a big-bang rewrite.
- Treat the legacy database as a compatibility constraint, not the target.
- Keep authentication, authorization, auditing, and privacy first-class.
- Prefer a module over a service unless independent deployment or scaling pays
  off.
- Use only synthetic or sanitized data in development and evaluation.
- Make every step observable, testable, and reversible.

## Current scope: portfolio vertical slice

**Goal:** One runnable, tested, and documented slice showing patient registration
and encounter creation, reached through an authenticated gateway, with OpenEMR as
a compatibility constraint.

**Deployable pieces:** AuthFacade, API gateway, Patient Management, Clinical Care.

**Estimate:** roughly 4–6 months part-time. Re-estimate after M2 and M3.

### Status

| # | Milestone | Status | Evidence |
|---|---|---|---|
| M0 | Local stack in one Compose project | Done | Oct 4, Oct 10, Oct 11 |
| M0a | Database baseline, reproducible and tested | Done (cleanup pending) | Oct 4 |
| M0b | AuthFacade login through OpenEMR's OAuth server, facade-issued JWT | Done | Oct 10, Oct 11 |
| M0c | Black-box integration tests with readiness gate | Done | Oct 11 |
| M1 | Harden the identity bridge | Done | Oct 10, Oct 11, Oct 21, Oct 24, Oct 29, Oct 30, Oct 31, Nov 1, Nov 2 |
| M2 | Schema inventory for slice contexts | Done | Oct 4 (entities scaffolded), Nov 13 (inventory) |
| M3 | Gateway and token validation | In progress | Nov 13 |
| M4 | Patient Management service | In progress | Nov 14 (registration, duplicate detection, insurance, advance directives, and the FHIR Patient API all done; the FHIR API now uses the real Firely .NET SDK with US Core validation, ADR 0052; insurance swap is a multi-entity aggregate, ADR 0053; only patient merge remains, deferred to M5+, ADR 0051; WebApp frontend for the patient list, patient view, and registration merged Nov 16, PR #73) |
| M5 | Clinical Care service | In progress | Nov 16 (slices 1 Create Encounter, 2 Record Vitals and 3 relay + `PatientRegistered` consumption built, tested and verified on the running stack; legacy's own `EncounterService` and `VitalsService` read the rows; the `patient_data` stand-in is removed; shadow comparison against legacy creation done: 142 columns identical, 2 differ by documented rule; layered review done 2026-11-17 (`docs/clinical-care/04-layered-review.md`): 7 defects found and fixed; remaining: browser check of the legacy UI (needs a login); WebApp screens for encounters and vitals built Nov 17, ADR 0067, not yet checked in a browser) |
| M5b | Clinical Care: update, delete, audit, lookups | In progress | Nov 17 (phase 0 research and ADRs 0068, 0069 written; no code yet) |
| M6 | Portfolio polish | Not started | — |

**Done beyond the original plan:** a privilege boundary that limits the
AuthFacade client to authentication scopes, build isolation for the AuthFacade
container, and a readiness gate that stops integration tests from running
against a half-started stack.

### M0a: Database baseline (done, cleanup pending)

- Schema and seed data from the real OpenEMR installer, dumped from MariaDB 12.3
- Loaded automatically on first start; schema-only dump kept as a diff target
- Import test (`Db.Tests`) checks table count and that key seed tables are not
  empty

**Still to do before sharing:** remove the `Administrator` user and hash,
log and session rows, and environment-specific `globals`; confirm there are no
patient rows; have the admin created from environment variables on first run.

### M0b: AuthFacade login (done)

- `POST /connect/token` verifies credentials through OpenEMR's password grant
  and returns a facade JWT in the response body (superseded: see ADR 0013, which returns OpenEMR's token response;
  the endpoint itself and bearer authentication are later removed entirely by ADR 0035)
- `GET /me` validates the facade JWT and returns the subject and name (superseded: `/me` now validates the session cookie only, ADR 0035)
- Identity stays in OpenEMR; clients receive OpenEMR's token, verified before it is returned (ADR 0013, superseded by ADR 0035)

**Decision recorded:** the response is JSON, not a browser redirect. The
redirect and cookie handling considered earlier were out of scope here, and
were built later, under M1: the authorization-code flow with PKCE (ADR 0024,
ADR 0026) and the resulting session cookie (ADR 0027).

### M0c: Integration tests (done)

- `OpenEmrNext.AuthFacade.IntegrationTests` runs against the live stack and covers the seed,
  OpenEMR's OAuth server, the password grant, and `/me` signature validation (the password-grant
  and `/me` bearer coverage was removed with the password grant itself, ADR 0035)
- The readiness gate checks four conditions under one deadline before tests run

### M1: Harden the identity bridge (done)

**Done**

- Credentials are never stored or logged by AuthFacade
- Client scopes are minimal
- Integration tests verify signatures and the subject claim
- Layer dependency rules enforced by architecture tests (ADR 0007, narrowed by ADR 0035)
- Login credential rules follow OpenEMR's limits, not a new-account policy (ADR 0008, superseded by ADR 0035)
- Malformed OpenEMR success responses return 502 instead of 500 (superseded by ADR 0035: the endpoint is removed)
- Password plaintext is retrieved once and cleared (ADR 0009, superseded by ADR 0035)
- Login rate limiting per client address (ADR 0018, superseded by ADR 0035)
- The token endpoint accepts a refresh grant, so a bearer-token client can
  redeem OpenEMR's refresh token instead of re-authenticating (ADR 0023, superseded by ADR 0035)
- Browser login via the authorization-code flow with PKCE, driven by
  OpenIddict.Client against OpenEMR's own OIDC server (ADR 0024, ADR 0026)
- The real OpenEMR access/refresh token persists inside the session cookie's
  own encrypted ticket, instead of being discarded after login (ADR 0027)
- A BFF proxy (YARP) lets a browser session reach OpenEMR's API with that
  token attached server-side, so the browser itself never holds it (ADR 0029)
- Start-up ordering: Compose waits for OpenEMR's own readiness probe before
  starting AuthFacade's container, and AuthFacade's own startup polls the same
  probe as a second line of defense outside Compose (ADR 0036)
- A request body proxied to OpenEMR never appears in any log, across every
  logger category down to Trace — AuthFacade calls no logger directly, and
  this guards against a regression from the framework or a library
- `/oidc/login` and `/callback` are rate limited per client address, one
  shared budget across both steps of the flow (ADR 0018's design, resurrected
  by ADR 0038 after ADR 0035 removed it along with the password grant)
- HTTPS for the service itself, with a redirect and HSTS: the gateway
  terminates TLS with a local dev cert and AuthFacade trusts its forwarded
  headers (ADR 0039), so the session cookie's Secure flag — code already
  written to expect this (ADR 0024/0026) — is finally true end to end
- Logout ends both AuthFacade's and OpenEMR's own session via RP-initiated
  logout (ADR 0040) — the `end_session_endpoint` ADR 0024/0026 left
  unconfirmed turned out to exist, and its exact contract (a required
  `id_token_hint`, an exact-match `post_logout_redirect_uri`) is now handled
- Circuit breakers guard the token exchange, the JWKS fetch and the BFF
  proxy's forwarded calls (ADR 0041), resurrecting ADR 0017's design for the
  two call sites that exist after ADR 0035 removed the ones it originally
  guarded; verified live by stopping the openemr container mid-session
- Audit log of login outcomes and BFF proxy access, with no credentials or
  tokens in the log — scoped there instead of `/me` (ADR 0042): the BFF
  proxy is where a signed-in session actually touches OpenEMR's data, at a
  fraction of `/me`'s per-page-load volume
- The OpenIddict state-token store and the Data Protection key ring behind the
  `AuthFacadeSession` cookie now persist in a dedicated MariaDB database,
  separate from OpenEMR's own (ADR 0043) — closes two of the three gaps ADR
  0028 named blocking more than one AuthFacade instance; a container restart
  no longer silently logs out every signed-in session, the practical problem
  that motivated this over the state-token store's own narrower ~5-minute
  login-window gap
- The `AuthFacadeSession` cookie now carries only an opaque key; the actual
  ticket — claims and the real OpenEMR access/refresh token inside them (ADR
  0027) — lives in a server-side record in the same database (ADR 0043), via
  ASP.NET Core's own `ITicketStore` extensibility point (ADR 0044). A session
  can now actually be revoked: `POST /sessions/revoke-all` ends every session
  for the caller's own subject, the self-service answer to a suspected stolen
  cookie that ADR 0028/0040 both named as missing — no cooperation needed
  from whichever browser holds the stolen copy
- The BFF proxy silently refreshes an expired access token against OpenEMR's
  refresh grant before the next proxied call needs it (ADR 0045), closing
  the gap ADR 0029 explicitly deferred — the browser never notices. A failed
  refresh falls back to the stale token rather than failing the request;
  OpenEMR's own 401 decides, the same passthrough every other call already
  uses
- Admin-initiated session revocation (ADR 0045): a config-driven allowlist of
  OpenEMR subjects (`Admin:Subjects`, empty by default) can revoke any other
  subject's sessions via `POST /sessions/revoke/{subject}` — the admin-action
  half of the gap ADR 0028/0040 both named, left open by ADR 0044's
  self-service-only `/sessions/revoke-all`. Logged to the audit trail with
  who did it and against whom
- OpenIddict's own encryption/signing certificates are now persisted files too
  (ADR 0046), the last of the three gaps ADR 0028 named — closed the same
  way ADR 0039 already closed it for the gateway's own TLS certificate.
  Verified live: a state token encrypted before restarting `auth-facade` was
  still correctly decrypted after the restart, proceeding all the way to a
  real token request to OpenEMR
- Unit tests (81), architecture tests (1) and live integration tests (5) pass
  (down from this bullet's original counts: ADR 0035 removed the password
  grant, its tests, and the Domain/Application/Ports/Infrastructure layers)
- ADR: identity bridge design (ADR 0047) — the capstone synthesizing every
  ADR above into one description of the finished bridge, its guarantees, and
  its accepted risks (below). Merged, satisfying this milestone's exit
  criterion.

**Accepted risks** (recorded, not fixed — see ADR 0047 for the full list)

- Neither a refresh nor a revocation reaches OpenEMR's own side: it exposes
  no server-to-server token revocation endpoint (confirmed against its own
  discovery document and source, ADR 0044), so the underlying OpenEMR
  refresh token is never invalidated there
- Running more than one AuthFacade replica is still untested. ADR 0046
  closed the last structural blocker ADR 0028 named against it, but closing
  the blockers is not the same as proving it works
- Authorization for OpenEMR data, and for admin-initiated session revocation
  specifically, is not built — `/api/v1/me` confirms identity only, and
  `Admin:Subjects` is a fixed allowlist, not a real authorization model

**Exit criteria:** The security items above are either done or recorded as
accepted risks. The ADR is merged. **Met.**

### M2: Schema inventory (done)

**Done:**

- EF entities scaffolded from the live database.
- Full inventory against `snapshots/db.sql` (283 tables, parsed directly): the in-scope table → bounded
  context map, adjacent-but-deferred tables named explicitly, cross-context foreign keys with their
  reference/duplicate/API-call decision, and five legacy anti-patterns recorded as ADR input — see
  `docs/m2-schema-inventory.md`. Turned out not to need the full 3–4 week time-box: M4/M5 had already
  scoped the slice down to four tables (`patient_data`, `form_encounter`, `form_vitals`, `forms`), not the
  full schema.

**Decision gate:** Confirm the Patient–Encounter boundary. **Met** — `patient_data` vs. `form_encounter` +
`form_vitals` holds as a clean split; nothing in the inventory forces a different one. One open question
is handed to M4: which of `patient_data.id`, `.pid`, or `.uuid` the `Patient` aggregate's public identity
should actually use (`.id` carries no real uniqueness constraint in the legacy schema — see the inventory
doc's anti-pattern #2).

### M3: Gateway and token validation (in progress)

**Done, ahead of this milestone formally starting (landed during M1):**

- YARP gateway routing to AuthFacade and to WebApp (ADR 0033)
- Compose entry for the gateway (ADR 0033/0039)

**Done:**

- ADR 0048 (accepted): AuthFacade stays the permanent legacy bridge; no dedicated identity service is
  built in this scope. Gateway token validation is explicitly deferred until a real bearer token reaches
  the gateway (two named triggers in the ADR), rather than attempted against a guessed contract.
- Gateway integration test coverage: 5 black-box tests covering routing, TLS redirect, and HSTS
  (previously zero, unlike AuthFacade's unit/architecture/integration projects).

**Done:**

- Route to Patient Management (`/api/v1/patients/{**catch-all}`, `/fhir/{**catch-all}`), with a
  request transform attaching the shared `X-Internal-Gateway-Secret` server-side (ADR 0055, ADR 0060)
  — not a per-user auth decision, the one thing ADR 0055 already scoped to this gateway. Verified
  against the real running stack, including 2 new `GatewayStackTests`.

**Remaining — blocked on M5 existing, nothing to build yet:**

- Route to Clinical Care, once that service exists
- AuthFacade's BFF proxy gaining a cluster pointed at this gateway (ADR 0030) — a different mechanism
  (browser calls that need the real OpenEMR token forwarded through AuthFacade's own proxy) than the
  direct Patient Management route above, still unbuilt

Gateway token validation itself stays deferred per ADR 0048 until one of its two named triggers occurs —
not tracked as remaining M3 work, since there is nothing to build against yet.

**Decision gate:** Record the identity architecture in an ADR. **Met** (ADR 0048, accepted).

### M4: Patient Management service (in progress)

**Done:** registration (below), plus all four remaining slices of the full legacy parity plan's scope
expansion (ADR 0051) — duplicate detection, insurance data, advance directives, and the FHIR Patient
API, each its own hexagonal slice. **Remaining:** patient merge, deferred until M5+ exist (it reaches
into tables those services own, and isn't transactional in legacy — a risk not to copy as-is). See
`docs/m4-full-legacy-parity-plan.md` for the researched detail and build order.

Writes directly into OpenEMR's own `patient_data` table (ADR 0049) — no new database of its own. OpenEMR's
legacy PHP registration UI is retired once this ships: the new service becomes the only writer, so there
is no concurrent-writer race to design around.

- A slim EF model mapped onto the existing `patient_data` table (not owned or migrated by this service —
  the table already exists)
- `Patient` aggregate and registration use case
- Anti-corruption adapter for `patient_data` — reads **and writes** (no longer read-only, per ADR 0049)
- Transactional outbox publishing `PatientRegistered`, as a table inside the `openemr` database itself
  (ADR 0049) — not a separate store, so the insert and the outbox row commit together in one transaction
- A safe identity-allocation scheme for new rows (a real transaction or sequence) — not OpenEMR's own
  `getFreshPid()` (`SELECT MAX(pid)+1`, a read-then-write race — `docs/m4-domain-notes.md`). No longer a
  concurrency risk now that the legacy UI is retired, just a normal design choice to get right.
- Its own least-privilege MariaDB user, scoped to `patient_data` and its own outbox table only — not
  OpenEMR's existing broader credentials (ADR 0050)
- Unit and integration tests, via Testcontainers rather than the black-box-against-the-running-stack
  pattern AuthFacade/Gateway use (ADR 0050 — a deliberate departure, not an oversight)

**Exit criteria:** A patient is registered through the gateway. **Met** — PatientManagement is deployed
(Dockerfile, docker-compose service) and reachable through the Gateway (ADR 0060); verified against the
real stack by registering a patient through `https://localhost:7131` with no caller-presented
credential, the Gateway attaching the internal secret server-side. A first WebApp frontend slice
(the list, patient view, and registration pages, `OpenEmrNext.PatientManagement.Client`; see the frontend
section below) exercises the same path. The event
is published exactly once. Tests pass.

### M4 frontend: WebApp patient pages (done Nov 16, PR #73)

What the WebApp now does for patients, all through the gateway and the generated PatientManagement client:

- **List:** search by name (debounced, stale results ignored), sortable columns, 10/25/50 paging, retry on
  failure, and the public patient ID.
- **Patient view:** a basic-information banner above two tabs, Insurance and Advance directives, with counts in
  the tab labels. Each section loads its own data and has its own retry. Changes (add policy, swap levels, set
  status, upload document) open dialogs and only close on success. The breadcrumb shows the patient's name.
- **Registration:** sex dropdown, required date of birth that cannot be in the future, an unsaved-changes prompt,
  and it opens the new patient on success.
- **Resilience:** a per-dependency circuit breaker in the browser (the webapp's own `TransientFailureCircuitBreakingHandler`),
  verified with the backend down: an open breaker shows "temporarily unavailable, nothing was saved".
- **Accessibility:** heading structure, labeled actions, visible keyboard focus, and phone-width layout.

Verified by hand in the browser. Not yet verified: a successful submit of each change dialog (it would change
the dev database), the upload progress state, and skeleton placeholders on slow loads. There are no automated
webapp tests yet.

Open items, each with options and costs, are in `docs/patient-view-pending-decisions.md`. The ones that
block further M4 work are:

1. Recording directive changes under the signed-in person (the user ID is a placeholder today).
2. Raising the document upload limit (capped at 20 MB, base64 over JSON).
3. Whether `pubpid` serves as the MRN, or a separate MRN is needed.
4. Server-side paging and search for the patient list.
5. Webapp tests, which need an approval to run.

### M5: Clinical Care service (in progress)

- ~~Own database and migrations~~ **Superseded by ADR 0064:** writes into OpenEMR's own database through
  a least-privilege user, like M4 (ADR 0049); the legacy encounter screen stays live, so creation is the
  only write in slice 1 (shared `sequences` counter keeps encounter numbers safe)
- `Encounter` and `Vitals` aggregates
- Anti-corruption adapter for `form_encounter`, `forms` and (slice 2) `form_vitals`
- Consumes `PatientRegistered`: a relay in PatientManagement pushes outbox events over HTTP to a
  secret-gated Clinical Care endpoint, behind an `IEventPublisher` port; idempotent on event id
- Shadow comparison: legacy and new encounter creation match for the synthetic
  scenarios

**Slices (ADR 0064, MIG-10), all built:** 1 Create Encounter (`docs/clinical-care/01-create-encounter-slice.md`),
2 Record Vitals (`02-record-vitals-slice.md`, ADR 0065), 3 relay and `PatientRegistered` consumption
(`03-patient-registered-consumption.md`, ADR 0066), which removed the interim `patient_data` lookup. **Shadow comparison done**
(`01-create-encounter-slice.md` section 12) and **layered review done** (`04-layered-review.md`). **Remaining:** the legacy-UI check.

### M5c: Clinical Care problem list and allergies (phase 0 done Nov 17; branch `feat/m5b-update-and-audit`)

Both are rows of legacy's one `lists` table (a free-text `type`), so this slice is mostly about ownership and about three legacy writers that disagree.
Research: `docs/clinical-care/07-problems-and-allergies-research.md`; ADR 0070 (Proposed).

- **Slices:** 1 record, get and list problems (**done Nov 17**; suites green: unit 362, architecture 39, integration 330; `08-record-problem-slice.md`); 2 the same for allergies (**done Nov 17**; suites green: unit 407, architecture 45, integration 374; `09-record-allergy-slice.md`); 3 update (resolving a problem is an update) (**done Nov 17**; suites green: unit 473, architecture 45, integration 443; `10-update-problem-and-allergy-slice.md`); 4 link an issue to an
  encounter (**done Nov 17**; suites green: unit 494, architecture 49, integration 483; `11-link-issue-to-encounter-slice.md`); 5 WebApp tabs (**built Nov 17**, ADR 0071, builds with no warnings, **not yet checked in a browser**); 6 shadow comparison and review (**done Nov 17**, `12-shadow-comparison-and-review.md`: 215 of 229 columns identical, the 14 that differ explained; one defect found and fixed). **Remaining: the legacy-UI check and a browser check of the new screens, both needing a signed-in user.**
- **Reuses** the M5b machinery unchanged: `modifydate` as the `ETag` token, `If-Match` (428, 412), the append-only audit and the outbox.
- **Out of scope:** delete (waits for the permission port), medications, surgeries, dental issues, devices and health concerns (other owners).

### M5d: finish Clinical Care (done Nov 17, branch `feat/m5d-clinical-care-completion`; not yet merged)

Fills the remaining Clinical Care functionality that needs no new service and no verified identity. Slices, each researched, built and tested in turn:

1. **Correct vitals** (**done Nov 17**; suites green: unit 533, architecture 53, integration 545; `13-correct-vitals-slice.md`).
2. **Immunizations** (**done Nov 17**; suites green: unit 616, architecture 59, integration 623; `15-record-immunization-slice.md`, ADR 0072).
3. **SOAP notes** (**done Nov 17**; suites green: unit 650, architecture 63, integration 671; `17-soap-notes-slice.md`, ADR 0073): addressed by form id within the encounter, versioned by content. 3b. **Clinical notes** (**done Nov 17**; suites green: unit 685, architecture 69, integration 760; `19-clinical-notes-slice.md`, ADR 0074): own parent form per note, its id allocated under a database lock; linked documents and results not covered.
4. **Lookups** (**API done Nov 17**; suites green: unit 650, architecture 63, integration 692; `18-lookups-slice.md`): classes, facilities and calendar categories. **Wired into the WebApp's `CreateEncounterDialog`** with a fallback to the old fields; providers wait for identity. The gateway routes for `/api/v1/immunizations` and `/api/v1/lookups` had been missing and were added, with a guard test.
5. **Providers lookup** (**M5f, done Nov 17; suites green: unit 688, architecture 69, integration 805**; `18-lookups-slice.md` section 5): the picker for naming a provider other than yourself. Widens Clinical Care's `users` grant by `fname`, `lname`, `authorized` (user's decision).
6. **Permission port and delete** (**M5g, API done Nov 17**; `21-permissions-and-delete.md`, ADR 0076): `IAccessControl` reads legacy's own `gacl_*` rules (admin/super), and `DELETE` on vitals, SOAP notes and clinical notes flags them as legacy does (`forms.deleted`, note `activity`), conditional on `If-Match`. WebApp: a Delete button with a confirmation on the vitals, SOAP and clinical note panels (compile-checked only; a 403 is explained as "only an administrator can delete"). **Not covered:** problems and allergies (waiting on a user decision: legacy hard-deletes them and its screens do not hide an inactive one).
7. **Idempotency keys and the SOAP retry guard** (**M5h, done Nov 17**; `22-idempotency.md`, ADR 0077): `Idempotency-Key` on every creating `POST` (replay, 409, 422, 24-hour window, own table), and a SOAP note no longer duplicates when its transaction is re-run after a lost commit acknowledgement. **Not done:** the WebApp does not send keys yet; the OpenAPI document does not declare the header; Patient Management has no idempotency.
- **Out of scope:** editing `sensitivity` (still needs its own permission decision).

### M5e: FHIR for Clinical Care (not started)

Clinical Care has no FHIR surface; only Patient Management does (`Patient`, Firely SDK with US Core validation, ADR 0052). Legacy exposes read-only FHIR for these resources, which are the parity targets:

| Resource | From | Legacy FHIR |
|---|---|---|
| `Encounter` | encounters | `GET /fhir/Encounter` |
| `Condition` | problems | `GET /fhir/Condition` |
| `AllergyIntolerance` | allergies | `GET /fhir/AllergyIntolerance` |
| `Observation` (vital signs) | vitals | `GET /fhir/Observation` |
| `Immunization` | immunizations | `GET /fhir/Immunization` |

- **Read-only first** (search by patient, read by id), mapped with the Firely SDK as Patient Management does, validated against US Core, with a `/fhir/...` gateway route to Clinical Care. Writes stay on the JSON API.
- **Needs an ADR first** (scope creep rule): resource identity (problems, allergies and immunizations have uuids; vitals and SOAP notes map differently), one `meta.lastUpdated` per source (a stamp, a content version, or none), and how the FHIR `ETag` relates to the JSON one.
- **Depends on** the gateway fix in M5b phase 1b: a clinical FHIR read must not be reachable without sign-in.


### M5 frontend: WebApp encounter pages (built Nov 17, ADR 0067)

All through the gateway and the generated ClinicalCare client, in the same style as the patient pages:

- **Patient view, Encounters tab:** server-paged list, **New encounter** dialog, and a link into each visit. Creating one opens it.
- **Encounter page:** the visit's details and a Vitals card (server-paged, US/metric switch, **Record vitals** dialog showing the derived BMI).
- **Known gaps:** the provider id is a placeholder (`1`), facility and calendar category are free numeric fields (no lookup endpoints), and
  date-only fields can shift a day when browser and server zones differ (`docs/patient-view-pending-decisions.md`, items 1, 9, 10).
- **Verification:** builds with no warnings. **Not yet run in a browser**; no automated webapp tests (item 7).

### M5b: Clinical Care update, delete, audit and lookups (in progress, branch `feat/m5b-update-and-audit`)

M5 proved the slice pattern but both aggregates are create-only. M5b closes that. Research: `docs/clinical-care/05-update-and-audit-research.md`.

- **Phase 0 (done Nov 17):** writer inventory from the running 8.0.0 container (21 files write `form_encounter`), the concurrency token
  (`last_update` / `last_updated`, `ON UPDATE CURRENT_TIMESTAMP`, whole-second), the editable column set, legacy delete behaviour.
  ADR 0068 (conditional updates on owned columns), ADR 0069 (own append-only audit table), both Proposed.
- **Phase 1a (done Nov 17):** `IAuditLog` port, `clinical_care_audit` table and grants, adapter, audited creates and audited refusals (built Nov 17; Clinical Care suites green: unit 236, architecture 31, integration 192); `ETag` on single-record reads built (suites green: unit 256, architecture 31, integration 196). `If-Match` (412, 428) and the update and delete events arrive with the endpoints that use them (phases 2 and 3). Phase 1a is otherwise done.
- **Phase 1b (ADR 0075):** gateway validates the session and forwards a verified actor; fixes F-CC-1, the placeholder provider and the open patient API. **Part 1 done Nov 17:** the gateway requires a signed-in session for Clinical Care and Patient Management and forwards the verified user (`ProxyTests`, 26). **Part 2 done Nov 17:** Clinical Care requires the verified user on every route and uses them as the acting provider and audit actor (a body `providerId` is ignored); no Clinical Care dialog sends a provider. Clinical Care suites: unit 688, architecture 69, integration 798. **Part 3 done Nov 17:** Patient Management does the same (unit 166, architecture 17, integration 86); no placeholder user id remains in the WebApp.
- **Phase 2 (done Nov 17; suites green: unit 300, architecture 33, integration 253):** update an encounter (`PUT`, `If-Match`, 412 and 428), `docs/clinical-care/06-update-encounter-slice.md`. **Phase 3a:** correct vitals (**done Nov 17**, M5d slice 1). **Phase 3b:** permission port, then soft delete.
- **Phase 4:** lookup endpoints (now M5d slice 4) and WebApp edit flows. **Phase 5:** shadow comparison of update, concurrency test, layered review.
- **Out of scope for good:** whole-encounter hard delete (`deleter.php` reaches billing). **Deferred:** idempotency keys (F-CC-8),
  sensitive-read audit, amendments with history.

**Exit criteria (M5):** An encounter is created for a registered patient and vitals
are recorded, end to end. The shadow comparison shows no unexplained
discrepancies.

### What remains before the post-M5 gate (as of Nov 17)

**Build (no identity needed):** M5e FHIR (read-only). M5d's API work and its WebApp screens (vitals correction, immunizations, SOAP notes, clinical notes, lookups; `20-webapp-clinical-screens.md`) are built, **but the screens are compile-checked only**.
**Identity (M5b phase 1b) is done for sign-in and for Clinical Care** (ADR 0075): the gateway requires a session, Clinical Care acts as the verified user. Patient Management consumes it too (registration, document owner, directive reviewer). **Still open:** delete for problems and allergies (decision pending) and editing `sensitivity`; a bearer-token path for external FHIR clients (deliberately not built until there is a client to design it for).
**WebApp tests (M5i, started Nov 17):** a bUnit project exists (24 tests, the three delete flows); the other screens have none yet.
**Gateway session check (M5j, analysis Nov 17):** one primary-key read and a small decrypt per request, no writes; no cache yet; a benchmark with a real session is owed (ADR 0075 amendment). **AuthFacade `Sessions` rows are deleted only when read again, so abandoned sessions accumulate**: a sweep is a small open item.
**Patient Management audit trail (M5k, Nov 17; `docs/patient-management/09-audit-trail.md`, ADR 0078):** its own append-only `patient_management_audit` table, written in each write's own transaction (register, add and swap insurance, upload a directive document, set the directive status); refusals are audited as failed. Reads and legacy's `log` table are not covered.
**Patient Management can correct a patient (M5r, Nov 18; `docs/patient-management/10-demographics-update.md`, ADR 0081):** name, date of birth, sex and emails, under a content-hash version, with a WebApp Edit dialog. Contact details and the FHIR `PUT` are not built.
**Verification not yet done:** a browser check of every WebApp screen (including all the new M5d ones); a legacy-UI check that records written here display correctly in OpenEMR itself; shadow comparison of the update paths, vitals correction and immunizations (only creation was compared); a layered review of M5b to M5d.
**New services, not started:** documents, pharmacy, labs, billing.
**Then:** the post-M5 gate, and M6.

### M6: Portfolio polish (not started)

- One-command startup for the full slice
- Architecture tests (ArchUnitNET) enforcing dependency rules
- Cross-service integration tests
- ADR index and updated README
- Demo walkthrough (script or recording)
- Case study: what worked, what did not, and the trade-offs

**Exit criteria:** A new reader can clone the repository, run the slice, and
follow the demo in under 15 minutes.

## Architecture Decision Records

ADRs are written alongside each milestone. The changelogs already contain the
reasoning for these decisions, so they should be converted rather than rewritten
from memory.

From 4 October:

- Baseline dump produced by the real installer, not hand-written SQL
- Entity model shared for now; per-service slim models before splitting

From 10 October:

- OpenIddict dropped in favour of delegating to OpenEMR's OAuth server
- Direct reads of OpenEMR's `users` table rejected
- Facade-issued JWTs instead of passing OpenEMR's token to clients
- Facade as adapter over OpenEMR, with OpenEMR left unmodified
- OpenEMR run as its own stack, not copied into the project
- Pinned release image over the development image
- Client secrets generated, never typed into chat

From 21 October:

- Layered AuthFacade structure, with dependency rules enforced by tests
  (superseded by ADR 0035: three of the five layers are removed)
- Login credential rules follow OpenEMR's limits; complexity policy deferred
  (superseded by ADR 0035)
- Password plaintext retrieved once; copies in memory accepted as a limit
  (superseded by ADR 0035)

From 11 October:

- One root Compose file for the whole solution
- One configuration source, gitignored secrets
- AuthFacade as a token broker, not an identity provider (superseded by
  ADR 0035: the password grant is removed; AuthFacade is a BFF, not a broker)
- OAuth client provisioned by an idempotent seed
- Minimal scopes for the AuthFacade client
- Black-box integration tests with a readiness gate
- Build isolation for the AuthFacade container

## Open questions

- **Ownership of the OAuth client:** OpenEMR owns the row today. If AuthFacade
  becomes a standalone identity service, ownership and the seed move with it.
- **Client-registration correctness:** The seed inserts a `oauth_clients` row
  directly. The 10 October decision noted that secret hashing varies between
  OpenEMR versions. The seed works with the pinned version, but this is an
  accepted risk, not a resolved one. Revisit it when the OpenEMR version changes.
- **Seed contents in images:** The seed holds environment configuration. Decide
  whether it belongs in build artifacts.
- **When to split the Compose file:** The trigger is a service that needs its own
  deployment target, not a service count.

## Risks

- **Upstream OpenEMR defect:** Some wrong-password login attempts return 500.
  No automated test guards against a regression since the password grant's own
  integration test was removed with it (ADR 0035). Track it upstream.
- **Brute force against login:** The password grant's per-client-address limiter
  (ADR 0018) is gone along with it (ADR 0035). Browser login goes through
  OpenEMR's own `login.php`, so OpenEMR's lockout is the only control.
- **M2 can expand:** Time-box it and accept a good-enough inventory.
- **Synthetic data quality:** M4 and M5 tests pass without proving much unless
  the data includes messy edge cases.
- **Solo capacity:** Each milestone should end in a working, demonstrable state.
  If time runs short, reduce M5 and M6 rather than leaving M0–M4 half-finished.
- **Scope creep:** Work outside the four deployable pieces needs an ADR first.

## Long-term phases

These are not scheduled for the current scope. Each assumes evidence from earlier
work and will be revisited at decision gates.

- **Phase 0, Foundation:** licensing, CI with formatting, static analysis, tests,
  and dependency scanning. Partly in place.
- **Phase 1, Discovery and baseline:** capability and workflow mapping, context
  map, glossary, legacy inventory, and baseline metrics. The slice's part of this
  phase is M2. Stakeholder interviews are needed before any production claim.
- **Phase 2, Platform architecture:** module rules, API and event conventions,
  identity and audit requirements, and the coexistence model. The slice exercises
  these in a limited form.
- **Phase 3, First vertical slice:** the current scope (M0–M6).
- **Phase 4, Controlled pilot:** canary, shadow, or parallel running with real
  users in a sanitized environment. Out of scope for the portfolio phase. The M5
  shadow comparison is the closest equivalent.
- **Phase 5, Production-capable capability:** hardened deployment, secrets and key
  rotation, backups, threat modeling, and on-call. Not planned for this scope.
- **Phase 6, Expand by context:** apply the approach to other contexts, selected
  by value, coupling, risk, and benefit.
- **Phase 7, Data ownership transition:** establish authoritative sources, sync
  rules, reconciliation, and retirement of legacy access paths.
- **Phase 8, Evaluation:** compare baseline, pilot, and post-migration metrics;
  record failed hypotheses; set the next strategy from evidence.

## Cross-cutting workstreams

- **Security and privacy:** threat modeling, dependency scanning, secrets,
  least-privilege access, authentication tests, de-identified data.
- **Clinical safety:** critical-workflow validation, safe failure, rollback,
  data-integrity checks.
- **Observability:** structured logs, health checks, correlation across legacy and
  new components. Metrics and tracing come later.
- **Documentation:** ADRs, glossary, context map, contracts, data-ownership
  records, changelogs, case study.

## Decision gates

Work can be paused or reversed when security controls are inadequate, critical
workflows are not equivalent, data consistency cannot be shown, reliability
regresses beyond tolerance, operational burden outweighs the benefit, coupling
becomes excessive, or rollback is impractical.

Named gates in the current scope:

- After **M2:** confirm the Patient–Encounter boundary.
- After **M3:** record the identity architecture decision.
- After **M5:** decide whether the slice meets its success criteria, then proceed
  to polish or revise the design.

The roadmap succeeds when it produces a safer, more maintainable platform and an
honest account of what the approach does and does not deliver.
