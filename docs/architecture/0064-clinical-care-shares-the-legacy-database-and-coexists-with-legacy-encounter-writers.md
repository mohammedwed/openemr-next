# 0064. Clinical Care shares the legacy database and coexists with the legacy encounter writers

- **Status:** Accepted
- **Date:** 2026-11-16
- **Builds on:** [0049](0049-patient-management-writes-into-the-legacy-database.md), [0050](0050-m4-testing-and-database-access.md), [0055](0055-patientmanagement-api-gated-by-a-shared-secret-identity-forwarding-deferred.md), [0060](0060-patientmanagement-is-deployed-and-reachable-through-the-gateway.md), `docs/m2-schema-inventory.md`
- **Rule(s):** KD-2, KD-4, LEG-8, LEG-12, LEG-15, EVT-1, EVT-8, MIG-6, MIG-10, MIG-11 (playbook `docs/microservice-playbook/`)

## Context

ROADMAP M5 (Clinical Care) says "own database and migrations, slim model from `form_encounter` and related
tables", and "consumes `PatientRegistered` through the outbox". Three facts make that wording unsafe to
follow literally:

1. **ADR 0049 already put the first service in OpenEMR's own database**, and M5's exit criterion
   ("an encounter is created ... shadow comparison shows no unexplained discrepancies") only means
   something if the encounter is visible to the legacy system (LEG-15). A separate database would need a
   sync or dual write just to be comparable.
2. **Unlike patient registration, no decision retires the legacy encounter screen.** `form_encounter` has
   at least six legacy writers (see `docs/clinical-care/01-create-encounter-slice.md` §2): the encounter
   form and REST API (`EncounterService::insertEncounter`), appointment-to-encounter
   (`library/encounter_events.inc.php`), document import (`controllers/C_Document.class.php`), HL7 results
   (two files), and group attendance. ADR 0049's "single writer" argument does not hold here.
3. **The outbox has no relay** (patient-management design set, decision D2), and the playbook says a
   consumer must never read the producer's outbox table (EVT-8). "Consumes through the outbox" therefore
   needs a delivery mechanism that does not exist yet.

The user decided all three on 2026-11-16.

## Decision

1. **Clinical Care writes into OpenEMR's own `openemr` database**, mapping `form_encounter`, `forms` and
   later `form_vitals` with `ExcludeFromMigrations` (LEG-1). It does not create or migrate those tables.
   Tables it owns outright (its outbox, its patient reference) are created by an idempotent seed script
   and a dedicated least-privilege user, exactly as ADR 0050 did (LEG-6, LEG-18).
2. **The legacy encounter screen stays live.** Clinical Care is one of several writers. This is safe for
   *creation* because encounter numbers come from the same shared `sequences` counter legacy uses
   (`QueryUtils::generateId()`, an atomic `UPDATE sequences SET id = LAST_INSERT_ID(id + 1)`), the idiom
   `EfAdvanceDirectiveRepository` already pins (LEG-12). Slice 1 therefore does **create and read only**.
   Update, billing-column changes and soft-delete are not taken over until the other writers are
   inventoried per column (LEG-8) and a concurrency token exists (LEG-10).
3. **Slice order is by risk (MIG-10):** slice 1 is *Create Encounter* through every layer, including
   grants, outbox, container and gateway route (MIG-6). Slice 2 is *Record Vitals*. Slice 3 is the relay
   and consumption of `PatientRegistered`.
4. **Event transport is an HTTP push behind a port.** A relay hosted service in PatientManagement drains
   `patient_management_outbox` and posts each event to a secret-gated internal endpoint on Clinical Care,
   through an `IEventPublisher` port so a broker can replace it without touching callers. Events get an
   envelope with `eventId` and `schemaVersion` (EVT-2). The consumer is idempotent on `eventId` (EVT-3).
   No message broker is added now.
5. **Interim stand-in (MIG-11).** Until slice 3, "does this patient exist?" is answered by an
   `IPatientDirectory` port whose adapter does a read-only lookup of `patient_data.uuid`/`pid`. The coupling
   stays inside that adapter (design set R10 pattern). **Removal criterion:** slice 3 is done and
   `clinical_care_patient_ref` is populated (events plus a one-time provisioning backfill); then the
   adapter, the `SELECT` grant on `patient_data` and its tests are deleted, and the coverage lost is
   recorded.
6. **Gateway and identity follow ADR 0055's mechanism, with a secret of Clinical Care's own.** The gateway
   attaches `X-Internal-Gateway-Secret` on the `clinicalcare` cluster only, deleting any caller-supplied value
   first (API-16). It is a different secret from PatientManagement's (`CLINICAL_CARE_GATEWAY_SECRET`), so a
   compromised backend cannot present itself to the other as the gateway. There is no per-user check at the
   gateway (the standing decision). The acting provider is therefore caller-supplied until identity forwarding
   exists, and is recorded as a tracked finding (F-CC-1), not hidden.
7. **The route is a flat `/api/v1/encounters`**, not nested under `/api/v1/patients/{uuid}`, because the
   gateway routes everything under `/api/v1/patients/` to PatientManagement and one prefix must belong to one
   service. The patient is named in the request body.
8. **Event naming follows the ubiquitous language of the blueprint** (`docs/openemr-ddd-design.md` §3.1):
   `EncounterCreated`, later `VitalsRecorded`.

## Consequences

- The shadow comparison (ROADMAP M5) is straightforward: run legacy and new creation against the same
  snapshot and diff the `form_encounter` and `forms` rows.
- Two writers means the new service must write rows byte-compatible with legacy (including the `forms`
  row, `uuid_registry`, denormalized facility name, default POS code) or legacy screens break. The manual
  "open it in the legacy UI" check (LEG-15) is a required step of the slice, not optional.
- A new risk is accepted: legacy and new creation can interleave. Compensating controls are the shared
  counter, tolerant reads (soft-deleted encounters are treated as absent), and no update path in slice 1.
- ROADMAP's "own database and migrations" bullet no longer applies; ROADMAP M5 is updated in the same
  change. The patient-management design set's "Clinical Care (M5), not built" and risk M4 ("event with no
  relay, blocks M5") are now scheduled work (slice 3), not open questions.
- Grants, a seed script, a compose service and a gateway route are added (details in the slice plan).

## Alternatives considered

- **Own database and sync (ROADMAP's literal reading).** Needs a dual write or CDC to keep legacy screens
  correct. Rejected: the cost lands in the first slice and proves nothing about the domain.
- **Shared database, retire the legacy encounter screen.** Cleaner concurrency, but removes a working
  screen with no decision to do so. Rejected for now; revisit when parity for update and billing is built.
- **Message broker now.** More realistic transport, but infrastructure before the first consumer exists.
  Rejected; the port keeps the option open.
- **Consume by reading `patient_management_outbox` directly.** Violates EVT-8 and couples two services at
  the table level. Rejected.
- **No events in M5, read `patient_data` permanently.** Breaks the "consumes `PatientRegistered`" bullet
  and the boundary from `docs/m2-schema-inventory.md`. Rejected as permanent; kept only as the interim
  stand-in above.

## Revisit when

- A second legacy writer's behaviour is found to conflict with a row the new service writes (reopen 2).
- Update or billing parity is scheduled (decide whether to retire the legacy encounter screen).
- A second consumer of any event exists (reconsider a broker, 4).
- Identity forwarding is built (replace the caller-supplied provider, 6).

## Related decisions

0049, 0050, 0055, 0060; `docs/patient-management/03-context-map.md` (R7, D1, D2, risk M4);
`docs/clinical-care/01-create-encounter-slice.md`; the layered review of slices 1 to 3: `docs/clinical-care/04-layered-review.md`.
