# Changelog 2026-11-13

## Summary

A large day spanning three roadmap milestones. Two CI reliability fixes unblocked the Gateway test
suite. M3 closed: ADR 0048 settles that AuthFacade stays the permanent identity bridge, with gateway
token validation deferred behind two named triggers, backed by Gateway's first integration tests. M2
closed: a schema inventory grounded in the real `snapshots/db.sql` dump, surfacing a real anti-pattern
in `patient_data` for M4 to avoid. M4 then began in earnest: the `Patient` aggregate and registration
endpoint were built, hardened into a full hexagonal (ports-and-adapters) shape, and its scope was
deliberately expanded — via ADR 0051 — from "just registration" to the full legacy patient-management
surface (minus the patient portal, with patient merge deferred until M5+ exists). Two of that plan's
four remaining features, duplicate detection and insurance data, were built and merged the same day.

## Fixed

- **CI never started the gateway or webapp containers.** The Gateway integration test project needs
  both running; CI only started `openemr`, `auth-facade`, and `phpmyadmin`, so every Gateway test
  failed with "Connection refused (localhost:7131)" — nothing was listening there. Also added a
  `/health` probe for the gateway alongside the existing OpenEMR/AuthFacade probes, for easier failure
  diagnosis.
- **The gateway crashed on startup in CI**, `FileNotFoundException` on `/certs/gateway.crt`: CI
  generated AuthFacade's own dev certs but never ran `scripts/setup/new-dev-cert.sh` for the gateway's
  (ADR 0039), so Compose reported it "Healthy" (it has no healthcheck defined) right before Kestrel
  failed to bind and the container exited.

## Added — M3: identity architecture settled

- **ADR 0048**, drafted and accepted: AuthFacade stays the permanent identity bridge; no dedicated
  identity service. Resolves ROADMAP M3's open "token validation at the gateway" question into two
  concrete deferred triggers, since no bearer token currently reaches the gateway on any path.
- **Gateway integration tests**, previously zero: cover the routing/TLS/HSTS behavior ADR 0033/0039
  already built.
- `ROADMAP.md` and the M2+M3 schedule doc updated for this week's reprioritization from M2 to M3, then
  corrected again once the Gateway test coverage above was actually built (it had been left under
  "Remaining").

## Added — M2: schema inventory closed

- **A schema inventory grounded in the real `snapshots/db.sql` dump** (283 tables, zero `FOREIGN
  KEY`/`CONSTRAINT` clauses anywhere), not guessed. Confirms the Patient–Encounter boundary holds, and
  surfaces a real finding for M4: **`patient_data` has no `PRIMARY KEY`**, and its `id` column carries
  no uniqueness constraint at all — `pid` and `uuid` are the only columns actually safe to reference.
- **M4 domain notes** (`docs/m4-domain-notes.md`): vocabulary and business rules pulled from the real
  `PatientService`/`PatientValidator`/`FhirPatientService` PHP source, not guessed. Resolves the M2
  inventory's open identity question — OpenEMR's own FHIR API already uses `patient_data.uuid` as
  `Patient.id`, settling what the new `Patient` aggregate's identity should be built on.

## Added — M4: Patient Management begins

- **ADR 0049**: Patient Management writes directly into OpenEMR's own `patient_data` table — no
  separate database. The anti-corruption adapter becomes read **and** write, and the transactional
  outbox table must live inside the `openemr` database itself for the insert-plus-outbox-row atomicity
  to hold. Two risks flagged for later (concurrent writers, safe `pid` allocation); the concurrent-
  writer risk was closed the same day once the user confirmed the legacy PHP registration UI is
  retired, not kept running alongside the new service.
- **ADR 0050**: Testcontainers for M4's integration tests (a deliberate departure from the
  black-box-against-the-running-stack pattern AuthFacade/Gateway use), and a new least-privilege
  MariaDB user scoped to `patient_data` and the new outbox table, matching AuthFacade's own
  dedicated-user precedent.
- **The `Patient` aggregate and registration rules** — pure domain logic, enforcing the same
  required/optional field rules `PatientValidator` does. Two real bugs fixed along the way, both
  caught against a real database rather than by reasoning alone:
  - A UUID byte-order mismatch: `Guid.ToByteArray()` uses .NET's mixed-endian layout, not the RFC 4122
    big-endian order PHP's `Ramsey\Uuid::getBytes()` actually stores — `Rfc4122GuidConverter` fixes it,
    with a hand-verified known value in its tests, not just a round-trip check.
  - `Pid` is generated eagerly, independent of `Id`: `pid` is `NOT NULL DEFAULT 0` with a `UNIQUE KEY`,
    and deriving it from `Id` after insert would reopen the exact kind of race `getFreshPid()` already
    illustrates the cost of.
- **`PatientManagementDbContext`** maps `Patient` + `OutboxMessage` in one `DbContext`, so one
  `SaveChangesAsync()` call makes the outbox pattern's atomicity hold for free. Two more real schema
  mismatches fixed against a real Testcontainers database: `email`/`email_direct` are `NOT NULL
  DEFAULT ''`, not nullable (a `ValueConverter` looked like the fix, but EF skips a converter whose
  source value is null — fixed by backing those properties with their own always-non-null fields
  instead); and `AssignGeneratedIdentity` was dead weight, since EF's own `ValueGeneratedOnAdd()`
  already populates `Id` via field access.
- **`seed-patient-management-db`**, a new one-shot Compose service: creates the outbox table and a
  least-privilege user with per-table grants (`patient_data`: select/insert/update; the outbox table:
  all), nothing else in the `openemr` database. Verified against the live stack via `SHOW GRANTS`,
  including idempotency on rerun.
- **The Composition project** binds and validates real config (`PatientManagementDbOptions`, every
  field required — unlike AuthFacade's optional-with-SQLite-fallback shape, this service has nothing to
  fall back to). Also fixed the MariaDB server version pin (11.0 → 12.3.3, matching the real
  `MARIADB_IMAGE`) in both this and the Testcontainers fixture, which previously just guessed.
- **`POST /api/v1/patients`**, the service's first callable endpoint: validates via `Patient.Register`,
  inserts the patient row and its outbox row in one transaction, returns 201 with the patient's `uuid`
  or 400 with validation errors. `registeredBy` is caller-supplied for now (no authentication wired in
  yet, an already-accepted gap inherited from ADR 0048).

## Changed — M4: hexagonal (ports-and-adapters) architecture

Four successive refactors turned the initial two-project shape into six, each verified behavior-
preserving by the full test suite passing unchanged before and after:

1. **Domain and Infrastructure split into their own projects** — Domain now has zero package or
   project references and zero EF/ASP.NET Core awareness.
2. **An Application layer extracted** — `RegisterPatientUseCase` now owns the orchestration that
   previously lived inline inside the HTTP endpoint; the endpoint became a thin adapter. Deliberately
   *not* modeled on AuthFacade's current collapsed two-project shape — that one only collapsed because
   the code it served (the password grant) was deleted, which doesn't apply here; a real domain
   service gets full layering justified on its own merits.
3. **Ports and adapters for persistence** — a new `Application.Ports` project (naming and dependency
   rules matching AuthFacade's own historical `Application.Ports`, ADR 0007/0010): `IPatientRegistry`,
   with `EfPatientRegistry` as its only adapter. `Application` now references `Domain` and `Ports`
   only, never `Infrastructure` directly.
4. **`LayerDependencyTests`** enforces this by reflection over each compiled assembly's reference
   list — a forbidden reference now fails the build even if the code never actually calls anything from
   it.

## Added — M4: full legacy parity plan

- **ADR 0051 / `docs/m4-full-legacy-parity-plan.md`**: legacy's duplicate detection, insurance, advance
  directives, FHIR Patient API, and patient-merge features researched against the real PHP source.
  Patient portal access excluded entirely (a separate authentication system, never scoped in
  `ROADMAP.md`). Patient merge deferred until M5+ exist (it dynamically reaches into tables those
  services own, and isn't transactional in legacy — a risk not to copy as-is). The remaining four
  features sequenced for build-out, one hexagonal slice at a time, the same pattern registration used.

## Added — M4: duplicate detection (1/4)

- **Weighted SQL comparison** mirroring legacy's `DuplicatePatientService` exactly (email/DOB/name/sex
  worth 6 points each, soundex-lastname 2, shared phone 1, max 27), run directly against `patient_data`
  — not through the `Patient` aggregate, since the real computation is a bulk comparison against every
  other row. `dupscore` stays out of the `Patient` aggregate entirely; it's a review-workflow column,
  not a registration invariant. Called synchronously right after registration, matching legacy's own
  behavior, never surfaced in the registration response.
- Caught a real test-isolation bug: the Testcontainers fixture shares one database across every test in
  a collection, so two tests reusing the same hardcoded email bled into each other's scores. Fixed by
  giving every scored field its own GUID per test.
- Merged (PR #56).

## Added — M4: insurance data (2/4)

- **`EffectiveInsuranceRule`**, a faithful port of legacy's `choose()`, including its tolerance for
  MySQL "zero dates" (`0000-00-00`) and partial dates (`2026-05-00`) that a permissive `sql_mode` let
  legacy store on pre-existing rows — decided to replicate fully, for true parity, rather than
  normalizing to clean nullable dates.
- **`AddInsurancePolicyUseCase`/`GetEffectiveInsuranceUseCase`/`SwapInsuranceUseCase`**, and
  `EfInsurancePolicyRepository` (raw SQL throughout, not an EF-mapped entity, so a pre-existing row's
  zero/partial date never reaches EF's own date parsing). `SwapInsuranceUseCase` replicates legacy's
  `9999-12-31` sentinel-date workaround for the `(pid, type, date)` unique constraint during a level
  swap.
- Caught a real bug against the real database: `AddAsync`'s `INSERT` and its follow-up `SELECT
  LAST_INSERT_ID()` aren't guaranteed to share a connection without an explicit transaction —
  `LAST_INSERT_ID()` is session-scoped. Fixed by wrapping both in a transaction.
- **`POST /patients/{pid}/insurance`, `GET .../insurance/effective`, `POST .../insurance/swap`** HTTP
  endpoints. Fixed a real gap while wiring these up: a bad `policyUuid` on swap previously threw an
  unhandled 500 — now a clear 404.
- The seed script now also grants the least-privilege user select/insert/update on `insurance_data`,
  verified against the live stack via `SHOW GRANTS`.
- Merged (PR #57).

## Tests

- Gateway: integration tests added where there were previously zero (routing/TLS/HSTS).
- PatientManagement grew from nothing to **77 tests** by the end of the day: 54 unit, 18 integration,
  5 architecture — covering registration, the outbox atomicity, duplicate-detection scoring, the
  effective-insurance rule, insurance persistence/swap, and the HTTP endpoints for both registration
  and insurance, all against a real Testcontainers MariaDB where persistence is involved.
