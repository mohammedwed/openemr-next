# M2 schema inventory: the slice's bounded contexts

Answers ROADMAP.md's M2 remaining items, against `snapshots/db.sql` (283 `CREATE TABLE` statements,
parsed directly — no ORM or admin tool involved).

## Scope

The slice's goal is "patient registration and encounter creation... with OpenEMR as a compatibility
constraint." M4 and M5 already name their own table scope explicitly:

- **M4 (Patient Management):** "a slim model scaffolded from `patient_data` only."
- **M5 (Clinical Care):** "a slim model from `form_encounter` and related tables," with `Encounter` and
  `Vitals` as the two aggregates.

So the in-scope tables are narrow by the roadmap's own design, not the full 283. This inventory maps
those, and explicitly lists what's adjacent but deliberately out of scope, rather than silently ignoring
it.

## Method

`snapshots/db.sql` was parsed for every `CREATE TABLE` statement's column and key definitions. Two facts
were confirmed this way rather than assumed:

- **Zero `FOREIGN KEY` or `CONSTRAINT` clauses exist anywhere in the schema.** Every one of OpenEMR's
  283 tables has none. All referential integrity is enforced in application code (PHP), never the
  database. This governs how any anti-corruption adapter reading these tables must treat them: a value
  that *looks* like a foreign key is a convention, not a guarantee.
- Column presence (`pid`, `encounter`) was checked directly against every table, not inferred from naming
  alone, to find real cross-table references.

## Table → bounded context map (in scope)

| Table | Context | Role |
|---|---|---|
| `patient_data` | Patient (M4) | The `Patient` aggregate's source. |
| `form_encounter` | Clinical (M5) | The `Encounter` aggregate's source. |
| `form_vitals` | Clinical (M5) | The `Vitals` aggregate's source. |
| `forms` | Clinical (M5) | A generic registry joining encounters to whichever form types were recorded against them — see anti-pattern 3 below. Needed to find a `form_vitals` row's encounter at all. |

**Identity** (`users`, `oauth_clients`) is already bounded by AuthFacade's own prior ADRs (0003: delegate
credential verification to OpenEMR; 0008: login rules follow OpenEMR's own limits) — not re-inventoried
here, since that boundary is already decided and unrelated to the Patient/Clinical split this milestone is
checking.

## Adjacent tables, deliberately out of scope for this slice

Real Patient- or Clinical-context tables that carry `pid` but aren't part of M4/M5's stated scaffold:
`insurance_data`, `history_data`, `patient_history`, `employer_data`, `care_teams`, `issue_encounter`
(problem list), `pnotes`, `patient_reminders`, and others. These matter for a real EHR but not for "a
patient is registered" and "an encounter is created... and vitals are recorded" — M4/M5's actual exit
criteria. Listed here so a future milestone doesn't have to rediscover them; not analyzed further, per
the roadmap's own time-box guidance ("a good-enough inventory is acceptable").

## Tables written by more than one context

None, among the in-scope set. Each of `patient_data`, `form_encounter`, `form_vitals`, and `forms` is
written by exactly one side of the Patient/Clinical split. The coupling between them is read-only
reference, not shared writes — see below.

## Cross-context foreign keys (convention only — no DB constraint enforces any of these)

| From | To | Decision |
|---|---|---|
| `form_encounter.pid` | `patient_data.pid` | **Reference ID.** Matches M4/M5's already-decided design: M4 publishes `PatientRegistered` via an outbox; M5 consumes it. Clinical Care holds a reference to Patient, not a duplicate of patient data and not a live API call per request. |
| `form_vitals.pid` | `patient_data.pid` | Same: reference ID, same reasoning. |
| `forms.encounter` | `form_encounter.encounter` | Intra-context (both Clinical-owned), but flagged because of anti-pattern 3 below — it's a business-key join, not `form_encounter.id`. |

**Open sub-question for M4, not resolved here:** *which* identifier should the reference actually be?
`patient_data.id`, `.pid`, and `.uuid` are three different candidates, and they are not equally safe — see
anti-pattern 2.

## Legacy anti-patterns recorded as ADR input

1. **No referential integrity at the database level, anywhere.** Confirmed directly against the dump: 0
   `FOREIGN KEY`, 0 `CONSTRAINT` clauses across all 283 tables. Any anti-corruption adapter reading these
   tables must validate at the application boundary; the database will not catch an orphaned reference.

2. **`patient_data` has no `PRIMARY KEY`.** Its `id` column (`bigint`, `AUTO_INCREMENT`) carries only a
   plain, non-unique `KEY` index — nothing in the schema stops a duplicate `id` value; uniqueness is an
   accident of how `AUTO_INCREMENT` happens to behave, not an enforced guarantee. The only column with a
   real `UNIQUE KEY` is `pid`, which is also what every other table actually joins on. **A new `Patient`
   aggregate's identity should not assume `patient_data.id` is safely unique** — `pid` is the column the
   rest of the legacy schema already trusts, and `uuid` (see #5) is a modern, already-unique alternative.

3. **`form_encounter` has two different "the encounter's identifier" columns, and the constrained one is
   not the one anything joins on.** `id` is a real `PRIMARY KEY`. But `forms` (and, historically, every
   other child table) references the separate `encounter` column instead — a plain integer protected only
   by a non-unique composite index (`pid`, `encounter`), not a uniqueness constraint. The identifier
   actually used for joins throughout the legacy system is the one the schema does the least to protect.

4. **Forms are joined to encounters through a generic polymorphic table, not direct foreign keys.** `forms`
   (`pid`, `encounter`, `formdir`, `form_id`) is how OpenEMR records which form types are attached to which
   encounter. `form_vitals` itself has **no `encounter` column at all** — finding which encounter a vitals
   row belongs to requires joining through `forms` by `formdir` and matching `form_id` to `form_vitals.id`.
   M5's `Vitals` aggregate needs to read through this indirection; there is no direct `encounter_id` column
   on the table it reads.

5. **Modern `uuid` columns already coexist with the legacy integer keys.** Both `patient_data` and
   `form_encounter` carry a `uuid` (`binary(16)`, real `UNIQUE KEY`) column, already adopted ahead of this
   migration — plausibly for FHIR/API use. This is a real, already-unique, already-present identifier that
   a new aggregate's public identity could be built on, instead of inventing a fourth identity scheme
   alongside `id`, `pid`/`encounter`, and `uuid` itself.

## Decision gate

**Confirmed: the Patient–Encounter boundary holds as the roadmap already scoped it.** `patient_data` vs.
`form_encounter` + `form_vitals` (joined through `forms`) is a clean split; nothing found here forces a
different one. M4 inherits one concrete open question from this inventory — which of `patient_data.id`,
`.pid`, or `.uuid` the `PatientRegistered` event and `form_encounter`/`form_vitals`'s reference should
actually carry — to resolve when it designs the `Patient` aggregate's public identity, not before.
