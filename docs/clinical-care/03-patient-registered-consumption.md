# Clinical Care, slice 3: the relay and `PatientRegistered` consumption

- **Status:** Built and verified. Decisions: [ADR 0066](../architecture/0066-patient-registered-reaches-clinical-care-through-an-outbox-relay-over-http.md).
- **Milestone:** ROADMAP M5. Closes the open items from [slice 1](01-create-encounter-slice.md): the `patient_data` stand-in (F-CC-5), and, in
  PatientManagement, F-CC-3, F-CC-7 and F-CC-9.
- **Method:** playbook MIG-14: the contract first, then the consumer, then the producer.

## 1. Why this slice

Slice 1 created encounters for a patient it could only verify by reading another service's table. This slice makes Clinical Care learn about
patients the way the design set always said it would (R7): from the event PatientManagement already writes, and keep **a reference, not a copy**.
It also gives the outbox the relay that design-set decision D2 said was missing.

## 2. The contract (written before the code)

**Transport:** `POST /internal/events` on Clinical Care. Hidden from the public OpenAPI document; no gateway route; accepts only
`X-Internal-Events-Secret` (not the gateway's secret). Delivery is **at-least-once**.

**Envelope** (EVT-2). Names are camelCase and pinned by tests on both sides:

| Field | Meaning |
|---|---|
| `eventId` | Stable per event; identical on every redelivery. The idempotency key (EVT-3). |
| `eventType` | `PatientRegistered` |
| `schemaVersion` | `1` |
| `occurredAt` | UTC |
| `subject` | the patient's uuid |
| `actor`, `causationId` | `null` today (no verified identity yet) |
| `correlationId` | for tracing |
| `payload` | `{ uuid, pid, firstName, lastName, registeredAtUtc }` |

**What the consumer reads:** `uuid`, `pid`, `registeredAtUtc`. It ignores everything else, including the names (EVT-5, design set D4). A tolerant reader:
unknown envelope or payload fields are fine.

**Responses:** `204` applied or already applied; `202` an event type this consumer does not handle (ignored, not rejected); `400` the event
itself is unusable (unsupported `schemaVersion`, no usable patient, no `eventId`); `401` not the producers' secret.

## 3. Rule catalogue

| Id | Rule | Where |
|---|---|---|
| EV-1 | An event is applied at most once: its id is recorded in the **same transaction** as its effect, and a repeat is a no-op that still answers success. | `EfPatientReferenceStore` |
| EV-2 | A patient already known is left alone; a **different** uuid claiming a held pid fails loudly (unique key) and leaves the event unprocessed. | `EfPatientReferenceStore`, `clinical_care_patient_ref` |
| EV-3 | A patient reference needs a non-empty uuid and a positive pid. | `PatientReference.Create` (one owner) |
| EV-4 | Unknown event types are acknowledged, unsupported schema versions are rejected. | `InternalEventEndpoints` |
| EV-5 | Delivery order is the outbox id order; a failure stops the pass; only a dead letter is skipped. | `RelayOutboxUseCase`, `EfOutboxRelayStore` |
| EV-6 | Back-off doubles from 5 s, capped at 5 min; dead-letter after 8 attempts or on a permanent rejection (400, 422). 401, 403, 404, 429 and 5xx are transient. | `RelayPolicy`, `HttpEventPublisher` |
| EV-7 | Rows are leased while delivered, so a crashed relay loses nothing and two relays never take the same row. | `EfOutboxRelayStore` |
| EV-8 | The event id is a pure function of the outbox row id. | `OutboundEventFactory.EventIdFor` |
| EV-9 | Clinical Care reads no `patient_data`; its principal has no privilege on it. | seed script, `LeastPrivilegeGrantTests`, `PatientBackfillTests` |

## 4. Producer changes (PatientManagement)

| Change | Evidence |
|---|---|
| Outbox relay (use case, ports, store, HTTP publisher, hosted service, options) | `RelayOutboxUseCaseTests`, `OutboxRelayTests`, `HttpEventPublisherTests`, `EventRelayOptionsTests` |
| **F-CC-7:** `users` grant narrowed to `(id, uuid)` | live `SHOW GRANTS`; FHIR `Patient` list and read still `200` |
| **F-CC-3:** `uuid_registry` rows for `patient_data`, `insurance_data`, `documents`, in the same transaction | `UuidRegistryTests`; live registry row for a new patient |
| **F-CC-9:** document id drawn from `sequences` in its own short transaction | `UuidRegistryTests` (12 concurrent uploads, distinct ids); all existing document tests |
| Outbox table gains `attempts`, `next_attempt_at`, `locked_until`, `dead_lettered_at`, `last_error` | seed script (idempotent `ADD COLUMN IF NOT EXISTS`) |

## 5. Findings

| Id | Finding | Sev | Status |
|---|---|---|---|
| F-PM-1 | **Eventual consistency window:** an encounter requested in the seconds between registration and delivery is a `404`. Visible and tested, not hidden. | L | By design |
| F-PM-2 | `drive_uuid` of a document is a different kind of `uuid_registry` entry (`document_drive`) and is still **not** recorded. | L | Open |
| F-PM-3 | `clinical_care_processed_events` and delivered outbox rows are never purged. | L | Open (needs a retention job, EVT-7) |
| F-PM-4 | The event id is derived from the outbox row id: if the table were recreated or ids reused, new events would collide with processed ones and be silently dropped. | M | Open; revisit if the outbox is ever rebuilt |
| F-PM-5 | The backfill copies patients that exist **at seed time**. A patient created by a legacy writer later is only picked up at the next `compose up`; legacy registration is meant to be retired (ADR 0049 #5), so this should not arise. | L | By design, tracked |
| F-PM-6 | There is no alert wired to a dead letter yet: it is an error-level log line with the row id, which an alert can be built on (OPS-11). | M | Open |
| F-PM-7 | Delivery is one ordered stream by outbox id, not per-subject (EVT-4); fine for one event type, to revisit with more. | L | Open |

## 6. Verification

Tests (run 2026-11-16, all green on first run for this slice):

| Suite | Result |
|---|---|
| Clinical Care unit | 160 / 160 |
| Clinical Care architecture | 31 / 31 |
| Clinical Care integration (real DDL, real seed script, production principal) | 139 / 139 |
| PatientManagement unit | 166 / 166 |
| PatientManagement integration (all pre-existing tests plus the new ones) | 73 / 73 |

Live stack, through the gateway:

| Check | Result |
|---|---|
| PatientManagement's **pre-existing, never-delivered PascalCase outbox row** | Delivered on first start with a stable v5 event id; the patient was already known (backfill), so **no duplicate reference** |
| Register a **new** patient, ask for an encounter at once | **404** (not yet known) |
| ...about 5 seconds later | **201**, encounter number 4 from the shared counter |
| `clinical_care_patient_ref` | `source = event` for the new patient |
| **Outage:** consumer stopped, a patient registered | Event stays in the outbox: 2 attempts, backing off, a readable `last_error` |
| Consumer restarted | Delivered about 9 s later; an encounter for the patient registered **during the outage** returns **201** |
| `clinical_care` grants | no grant on `patient_data` (the live `REVOKE` removed it) |
| `patient_management` grants | `users` on `(uuid, id)` only; `INSERT` on `uuid_registry` |
| FHIR `Patient` list and read | `200` on the narrowed `users` grant |
| New patient's uuid in `uuid_registry` | `patient_data` / `id` (F-CC-3) |

Not verified: a dead letter on the live stack (covered by the integration tests only); the legacy UI (needs a login).
