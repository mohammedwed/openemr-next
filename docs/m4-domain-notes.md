# M4 domain notes: ubiquitous language and business rules from the legacy system

Input to the `Patient` aggregate's design, pulled from the real OpenEMR PHP source
(`/home/mo/repos/openemr`), not guessed from the schema alone. Covers vocabulary and rules; a formal
bounded-context/class diagram is deliberately not attempted here — see the scope note at the end.

## Patient identifier glossary

OpenEMR's `patient_data` table carries four different things that could be called "the patient ID." They
are not interchangeable, and the legacy code treats them differently:

| Column | What it actually is | Who generates it, how |
|---|---|---|
| `id` | A `bigint AUTO_INCREMENT` with no real uniqueness constraint (M2 finding). Barely used. | The database, on insert. |
| `pid` | The internal business key almost everything else joins on (`form_encounter.pid`, etc.). | **`PatientService::getFreshPid()`: `SELECT MAX(pid)+1 FROM patient_data`.** Not a sequence, not transactionally safe — see anti-pattern below. |
| `pubpid` | "Public patient ID" — what front-desk staff see, e.g. a chart number. Defaults to the same value as `pid` if left blank, but can be overridden by staff. | Defaults from `pid` at insert; otherwise supplied by whoever registers the patient. |
| `uuid` | A real `UNIQUE KEY`, generated in application code (`UuidRegistry`) at insert time. | **This is what OpenEMR's own FHIR API already uses as `Patient.id`** (`FhirPatientService.php:214-215`: `$id->setValue($dataRecord['uuid'])`). `pubpid` and `ss` become FHIR `identifier` entries (MRN/SSN-style business identifiers), never the resource `id`. |

**This settles the open question the M2 inventory handed forward.** OpenEMR's own standards-facing API
already draws the exact line a new `Patient` aggregate needs: **`uuid` is the identity; `pubpid` (and
similar) are identifiers attached to that identity, not the identity itself.** The new aggregate should
follow the same split rather than inventing a different one.

## A legacy anti-pattern worth avoiding, not reusing

`getFreshPid()`'s `SELECT MAX(pid)+1` is read-then-write with no locking — two registrations landing in
the same instant can compute the same "fresh" `pid`. This is exactly the kind of legacy pattern the new
service must not carry forward: whatever identity scheme M4 mints for new registrations needs to be safe
under concurrent writes (a real `AUTO_INCREMENT` in its own database, or a generated UUID — not a
hand-rolled max-plus-one).

## Registration business rules (from `PatientValidator::configureValidator`)

Required on insert, with the exact constraint:

- **First name** (`fname`): 1–255 characters
- **Last name** (`lname`): 2–255 characters (note: 2, not 1 — a one-character last name is rejected)
- **Gender** (`sex` column, labeled "Gender" in the validator's own error messages — a naming mismatch
  worth deciding on deliberately for the new aggregate, not inheriting silently): 4–30 characters. Stored
  as free text, not a fixed enum, in the legacy schema.
- **Date of birth** (`DOB`): must parse as `Y-m-d`

Optional, but validated for shape if present:

- **Email**, **Trusted/Direct Email** (`email_direct`): must pass OpenEMR's own email validator if
  supplied at all

On update, every field above becomes optional except that the target `uuid` must already exist — update
and insert share one rule set, not two.

## Two more naming traps

- **`date` means "last updated"; `regdate` means "created."** `PatientService::databaseInsert()` sets
  both to the current timestamp on insert, but going forward `date` is the one that moves. A column
  literally named `date` is the *last-modified* timestamp, not *the* date of anything specific.
- **`created_by`/`updated_by`** store the acting user's ID from the session (`authUserID`), required for
  every insert (defaulted, never left null in practice) — relevant to M4's registration use case needing
  to capture who performed the registration, not just that it happened.

## Existing extension points (terminology continuity, not structure to copy)

OpenEMR already fires `patient.created` / `patient.updated` events (`PatientCreatedEvent`,
`BeforePatientCreatedEvent`, and the `Updated` equivalents) when a patient row changes — confirming "a
patient is created" is already a recognized concept in the legacy system's own language, which
`PatientRegistered` (M4's planned outbox event) is a clean continuation of. The legacy events just carry
the entire raw `patient_data` row as their payload, though — not a model to copy structurally; M4's event
should carry a deliberately curated set of fields, not the whole row.

## Resolved: M4 writes into the legacy database

**Settled 2026-11-13, [ADR 0049](architecture/0049-patient-management-writes-into-the-legacy-database.md).**
`Patient.Register()` writes directly into OpenEMR's own `patient_data` table — there is no separate
database for Patient Management. This means the anti-corruption adapter is read **and** write, not
read-only, and the transactional outbox lives as a table inside the same `openemr` database (required for
the insert-plus-outbox-row atomicity the pattern depends on). OpenEMR's legacy PHP registration UI is
retired once this ships, confirmed by the user — so the new service becomes the only writer, and the
concurrent-writer risk the ADR originally flagged doesn't apply. The `getFreshPid()` race noted above is
still worth avoiding (any ordinary safe allocation scheme works now that nothing else can race it), just
not for the reason originally thought.

## Insurance: effective-dated slots, not plain CRUD

Grounded in `InsuranceService.php` and the `insurance_data` table while building the second slice of
the full-legacy-parity plan.

- A patient has up to three independent "slots" — primary/secondary/tertiary — each holding a history
  of effective-dated policies, not just one row per level. `UNIQUE KEY (pid, type, date)` is what makes
  this a history rather than a single current value.
- **Which policy is actually in force at a level, on a given day, is a real rule**
  (`EffectiveInsuranceRule`, ported from `choose()`), not "the most recently added row." It compares
  start dates as stored text, not as parsed dates — load-bearing, because:
- **Legacy tolerates "zero dates" (`0000-00-00`) and partial dates (`2026-05-00`)** on pre-existing rows,
  a side effect of a permissive `sql_mode` legacy allowed historically. A new aggregate never *writes*
  one of these, but reading one back (a row this service didn't create) must not throw. **Decided
  2026-11-13: replicate this tolerance fully, for true parity**, rather than normalizing to clean
  nullable dates — a deliberate rejection of the "simpler" option.
- **`swapInsurance()`'s sentinel-date workaround is real, not a legacy bug to skip.** Trading which
  policy occupies which level can momentarily violate the `(pid, type, date)` unique constraint (both
  rows briefly wanting the same `(type, date)` pair mid-swap) unless the occupant is parked on a
  far-future date first. This already is transactional in legacy and stays that way here.
- **Scope reduction, decided deliberately, not an oversight:** `CoverageValidator`'s full validation
  context also requires state/country/sex/subscriber-relationship (option lists this service has no
  source of truth for) and a full set of subscriber demographics plus `accept_assignment`. Only the
  fields independent of those option lists are validated here.

## Advance directives: a document, not a dedicated record type

Grounded in `PatientAdvanceDirectiveService.php`, `advancedirectives.php`, and `Document.class.php`
while building the third slice. Two independent legacy facts get bundled under one feature name:

- **The status flags and the attached document are not reconciled by any legacy logic.** A patient can
  have `completed_ad = 'YES'` with zero attached documents, or an attached scanned living will with the
  flag still blank — legacy trusts staff to keep both accurate by hand, not something this service
  introduces.
- **`completed_ad` is a three-state field, not a boolean.** The edit screen's blank "SKIP" choice stores
  as the empty string — "staff hasn't said either way" is a real, distinct state from `NO`.
- **There is no dedicated "advance directive type" column anywhere.** Legacy classifies a document as
  Living Will / Durable Power of Attorney / DNR purely by matching keywords in the document's own `name`
  (or its category's name) — `determineAdvanceDirectiveType()`. A document this service creates has to
  bake the right keyword into its stored name for legacy's own code to classify it correctly too.
- **Only one category is actually seeded by default**: "Advance Directive" (id 6, `codes =
  'LOINC:42348-3'`) — confirmed against the real compose stack's database, not just the install script.
  No child categories per type exist out of the box.
- **`documents.id` is not `AUTO_INCREMENT`.** It's drawn from `sequences`, a single-row global counter
  (`QueryUtils::generateId()` / ADOdb's `GenID()`) shared by many legacy tables, not one per table —
  writing a new row anywhere that uses this counter has to draw from this exact same row, via the same
  `UPDATE ... SET id = LAST_INSERT_ID(id + 1)` idiom, to avoid colliding with a still-running legacy
  writer.
- **Local-disk storage path**: `{repository}/{pid}/{driveUuid}` (no extension), a *second*, separate
  UUID from the document's own `uuid` column. Legacy also supports CouchDB and a pluggable
  remote-storage hook — real options, but deployment-specific extras most installs don't use; **decided
  2026-11-14: local disk only**, the same path real installs actually take, not a reduced-parity call.

## Scope note

This is vocabulary and rules, not a formal bounded-context diagram or conceptual class model. Per the
earlier discussion: for one aggregate and one use case, the class shape is expected to emerge from writing
the registration use case itself, informed by the terms and rules above, rather than from a separate
upfront design document.
