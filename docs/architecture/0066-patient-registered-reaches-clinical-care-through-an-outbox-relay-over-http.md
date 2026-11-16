# 0066. PatientRegistered reaches Clinical Care through an outbox relay over HTTP, with a secret of its own

- **Status:** Accepted
- **Date:** 2026-11-16
- **Builds on:** [0064](0064-clinical-care-shares-the-legacy-database-and-coexists-with-legacy-encounter-writers.md) (decision 4 chose an HTTP push behind a port; decision 5 named the stand-in and its removal criterion), [0049](0049-patient-management-writes-into-the-legacy-database.md), [0055](0055-patientmanagement-api-gated-by-a-shared-secret-identity-forwarding-deferred.md)
- **Rule(s):** EVT-1 to EVT-8, KD-4, MIG-11, MIG-14, API-16, SEC-3, OPS-3, OPS-19 (playbook `docs/microservice-playbook/`)

## Context

Since M4, PatientManagement writes a `PatientRegistered` row to `patient_management_outbox` in the same transaction as the
patient (ADR 0049). **Nothing ever read it** (patient-management design set, risk M4 and decision D2). Clinical Care (slice 1)
could not ask "does this patient exist?" without a stand-in that read `patient_data` directly (ADR 0064 #5), which crosses the
table boundary the whole migration exists to respect.

Facts that shape the delivery:

1. The outbox payload predates any consumer: PascalCase names, no envelope, no event id, no schema version. The one row
   already in it must still be deliverable.
2. There is no message broker, and ADR 0064 decided not to add one before a consumer exists.
3. Delivery over a network is at-least-once at best; the consumer must tolerate repeats.

## Decision

1. **A relay in PatientManagement** (a hosted service, `OutboxRelayHostedService`) drains the outbox and pushes each event to
   Clinical Care, behind an `IEventPublisher` port. The HTTP adapter can be replaced by a broker without touching the relay or
   the use case (EVT-8). The relay is **off unless `EventRelay:Enabled`**; when on, a missing target or secret fails startup
   (OPS-3).
2. **Order, leases, back-off, dead letters (EVT-7).** Rows are claimed in id order inside one short transaction (`FOR UPDATE`,
   deliberately **not** `SKIP LOCKED`, which would let a second relay jump over rows the first holds). A claim is a lease, so a
   relay that dies loses nothing. The first row not yet due stops the claim, so a later event never overtakes an earlier one.
   Failures back off exponentially (5 s, doubling, capped at 5 min); after 8 attempts, or on a permanent rejection, the row is
   **dead-lettered**: skipped, logged at error level with its row id (the alert signal), and kept with `last_error`.
3. **The envelope is built at publish time**, from the row, by a pure function (`OutboundEventFactory`): `eventId`, `eventType`,
   `schemaVersion` 1, `occurredAt`, `subject`, `correlationId`, and the payload re-written with **pinned camelCase names**. Old
   and new rows therefore leave identically, and the writer did not have to change.
4. **The event id is derived from the outbox row id** (a name-based v5 uuid), so every retry and every relay instance sends the
   same id, which is what lets the consumer dedupe. This assumes the outbox is append-only and its ids are never reused.
5. **Which failures are worth retrying.** Only a rejection of the **event itself** (400, 422) is permanent. 401, 403, 404, 408,
   429 and 5xx are the consumer's deployment or configuration, which an operator fixes while the backlog waits, so they back
   off and recover; dead-lettering on a wrong secret would discard good events.
6. **Clinical Care consumes at `POST /internal/events`.** It is hidden from the OpenAPI document and the generated client, has no
   gateway route, and is authenticated by a **separate scheme and a separate secret** (`CLINICAL_CARE_EVENTS_SECRET`,
   `X-Internal-Events-Secret`). So: the gateway cannot deliver events, the relay cannot call the public API, and neither secret
   opens the other's door (API-16). A shared base class holds the one constant-time comparison.
7. **The consumer is idempotent on `eventId` (EVT-3).** The event id is inserted into `clinical_care_processed_events` and the
   effect applied in **one transaction**, so "processed" and "applied" are the same fact; a repeat is a `204` no-op; a failed
   event is not marked processed. An event type the consumer does not know is acknowledged (`202`) and ignored, so a producer can
   add types without breaking every consumer. An unsupported `schemaVersion` is a `400`, so the producer alerts instead of
   silently skipping.
8. **Clinical Care keeps a reference, not a copy.** `clinical_care_patient_ref` holds the patient's uuid, pid and where it came
   from (design set R7, decision D4). The consumer reads only `uuid`, `pid` and `registeredAtUtc` from the payload (a tolerant
   reader); the names the producer also sends are ignored.
9. **The stand-in is removed (ADR 0064 #5, MIG-11), on the day the criterion was met.** `EfPatientDirectory` reads the local
   table; the encounter reader joins it instead of `patient_data`; the `SELECT (uuid, pid)` grant on `patient_data` is
   **revoked** and a test proves the principal can no longer read it. Patients registered before events existed are copied once
   by the **seed script, as root** (`INSERT IGNORE ... SELECT`): provisioning, not a runtime read.
10. **Seed scripts start from nothing.** Both seed scripts now `REVOKE ALL PRIVILEGES, GRANT OPTION` before granting, because
    `CREATE USER IF NOT EXISTS` keeps whatever an older version granted: without it a grant removed or narrowed in a script
    would linger on every database that had already run it.

### Changes to PatientManagement made in the same change

- **F-CC-7:** its grant on `users` is narrowed from the whole table (which includes a `password` column) to `(id, uuid)`, the two
  columns the FHIR practitioner lookup selects.
- **F-CC-3:** it now records every uuid it creates in legacy's `uuid_registry` (`patient_data`, `insurance_data`, `documents`),
  in the same transaction as the row, as legacy's own `UuidRegistry::createUuid` does.
- **F-CC-9:** an advance-directive document's number is drawn from the shared `sequences` counter in its own short transaction,
  not held across the document insert, so the counter's row lock no longer serializes every legacy writer behind an upload.

## Consequences

- Clinical Care learns of a patient **eventually**, about one relay interval (5 s) after registration. An encounter requested in
  that window is a `404` and succeeds on retry. This is the price of not reading another service's table, and it is visible,
  tested and documented, not hidden.
- If the relay is down or the consumer is down, nothing is lost: the outbox keeps the events, and delivery resumes.
- Three secrets now exist (`INTERNAL_GATEWAY_SECRET`, `CLINICAL_CARE_GATEWAY_SECRET`, `CLINICAL_CARE_EVENTS_SECRET`); an existing
  `.env` needs the new ones added by hand.
- A dead-lettered event is skipped, so strict per-subject order can be broken by an operator-level event; it is logged loudly.
- `clinical_care_processed_events` grows without bound until a retention job exists (finding F-PM-3).

## Alternatives considered

- **A message broker now.** More realistic transport, but infrastructure before the first consumer was proven. The port keeps it open. Rejected for now.
- **Clinical Care polls PatientManagement's outbox.** Couples the services at the table level (EVT-8). Rejected.
- **Share one secret between the gateway and the producers.** Fewer variables, but a compromised producer could then call the public API as the gateway. Rejected (API-16).
- **Change the writer to emit the envelope.** Would leave the existing row undeliverable or require a data migration. Rejected: the envelope is built at publish time.
- **`SKIP LOCKED` claiming.** Higher throughput with several relays, at the cost of ordering. Rejected: registrations are rare and order matters more.
- **Keep reading `patient_data` as a permanent stand-in.** Rejected by ADR 0064; it is now gone.

## Revisit when

- A second consumer exists, or volume makes the poll interval or the single ordered stream a bottleneck (reconsider a broker and per-subject ordering, EVT-4).
- The producer's outbox may be recreated or its ids reused (the event id derivation breaks; store the id on the row instead).
- Identity forwarding exists (add the actor to the envelope).

## Related decisions

0049, 0050, 0055, 0064; `docs/clinical-care/03-patient-registered-consumption.md`; `docs/patient-management/03-context-map.md` (R7, D2, risk M4).
