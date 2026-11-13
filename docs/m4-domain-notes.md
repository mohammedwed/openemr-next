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

## Open question this doc does NOT resolve

M4's roadmap bullets say both "own database... scaffolded from `patient_data` only" **and** "read-only
anti-corruption adapter for `patient_data`." That's consistent with one reading — new registrations are
written into the new service's own database, and the read-only adapter exists only to let the new service
see patients that already exist in OpenEMR's legacy data (for coexistence, not for writing new rows back
into `patient_data`) — but this doc doesn't confirm that's the intended design. It matters directly for
the aggregate's shape (does `Patient.Register()` ever touch OpenEMR's own database at all, or only the new
one?) and should be settled explicitly before the aggregate is coded, not assumed from the bullet list.

## Scope note

This is vocabulary and rules, not a formal bounded-context diagram or conceptual class model. Per the
earlier discussion: for one aggregate and one use case, the class shape is expected to emerge from writing
the registration use case itself, informed by the terms and rules above, rather than from a separate
upfront design document.
