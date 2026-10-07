# Audit of `docs/legacy/` claims

## Scope and result

The requested directory [`docs/legacy/`](../docs/legacy/) does **not exist** in
this checkout. The legacy architecture documents produced in this workspace
are instead under [`legacy-diagrams/`](./).

Accordingly, no claims in `docs/legacy/` could be audited because there are no
files at that path. I audited the five Markdown documents currently present in
[`legacy-diagrams/`](./) as the likely intended scope:

- [`openemr-architecture-inventory.md`](./openemr-architecture-inventory.md)
- [`bounded-context-candidates.md`](./bounded-context-candidates.md)
- [`register-new-patient-trace.md`](./register-new-patient-trace.md)
- [`scheduling-cross-context-data-access.md`](./scheduling-cross-context-data-access.md)
- [`permissions-authentication.md`](./permissions-authentication.md)

This report distinguishes source-verifiable claims from analytical
classifications and from negative searches that are limited to the inspected
paths.

## Definite citation or factual problems

| Document claim | Finding | Evidence and required qualification |
|---|---|---|
| `portal/app.php` is a portal application entrypoint | **Not verifiable; file does not exist.** | The `portal/` directory exists, but [`portal/app.php`](../portal/app.php) does not. The architecture inventory should cite an existing portal bootstrap/entrypoint instead. |
| Scheduling appointment tables were not found or not established | **Contradicted by source.** | [`AppointmentService.php`](../src/Services/AppointmentService.php) declares and queries `openemr_postcalendar_events` and `openemr_postcalendar_categories`. The bounded-context document should list those tables as Scheduling-owned candidates. |
| API route examples use `{pid}`/`{eid}` interchangeably | **Route notation needs normalization.** | The registries use colon parameters such as `:pid`, `:eid`, `:puuid`, and `:muuid`; see [`_rest_routes_standard.inc.php`](../apis/routes/_rest_routes_standard.inc.php). Numeric IDs and UUID parameters should be distinguished. |

These are the only definite broken/nonexistent citation findings identified in
the audit. The first two concern the earlier architecture/context document;
the route notation issue is a presentation inconsistency, not proof that the
routes themselves are absent.

## Claims supported by source

### Patient registration

The central registration trace is source-supported:

- Branch selection uses `full_new_patient_form` in
  [`new.php`](../interface/new/new.php).
- Compact and comprehensive forms submit to their respective save handlers.
- `newPatientData()`, employer, insurance, and history helpers are in
  [`patient.inc.php`](../library/patient.inc.php).
- The compact path contains `REPLACE INTO patient_data`.
- Comprehensive settings such as simplified demographics and one-insurance
  behavior are in [`new_comprehensive_save.php`](../interface/new/new_comprehensive_save.php).

The registration trace correctly separates defined validator rules from rules
actually invoked by the comprehensive save path.

### Scheduling

The cited scheduling functions and tables exist in source:

- `DOBandEncounter()` in
  [`add_edit_event.php`](../interface/main/calendar/add_edit_event.php)
- `todaysEncounterCheck()` and `todaysTherapyGroupEncounterCheck()` in
  [`encounter_events.inc.php`](../library/encounter_events.inc.php)
- `manage_tracker_status()` in
  [`patient_tracker.inc.php`](../library/patient_tracker.inc.php)
- `fetchEvents()` in [`appointments.inc.php`](../library/appointments.inc.php)
- `getUserFacilities()` in [`calendar.inc.php`](../library/calendar.inc.php)

The cited scheduling SQL supports the listed patient, provider, facility,
encounter, group-encounter, and tracker accesses.

### Authentication and ACL

The authentication report's principal claims are source-supported:

- `confirmPassword()` and user-password verification are in
  [`AuthUtils.php`](../src/Common/Auth/AuthUtils.php).
- Passwords are loaded from `users_secure`.
- Session identity variables are populated by
  `AuthUtils::setUserSessionVariables()`.
- `authCheckSession()` and login/session handling are invoked through
  [`auth.inc.php`](../library/auth.inc.php).
- Password algorithm selection and verification are implemented by
  [`AuthHash.php`](../src/Common/Auth/AuthHash.php).
- `AclMain::aclCheckCore()` and the underlying
  [`Gacl::acl_check()`](../src/Gacl/Gacl.php) exist.

## Architectural classifications that are not explicit code claims

The following statements are useful analytical descriptions, but the source
does not declare them as formal architecture:

- “Scheduling-owned”
- “Patient registry”
- “Master data remains outside Scheduling”
- A table “belongs” to a bounded context
- A resource maps exclusively to one candidate context

SQL proves that a caller reads or writes a table. It does not prove formal
ownership. These statements should be phrased as:

> “Operational ownership inferred for analysis; not an explicit domain boundary
> in the code.”

This qualification applies especially to the ownership tables in
[`bounded-context-candidates.md`](./bounded-context-candidates.md) and
[`scheduling-cross-context-data-access.md`](./scheduling-cross-context-data-access.md).

Similarly, the architecture inventory's statement that the browser runtime is
“legacy-dominated” is a source-structure inference based on inspected
entrypoints/includes, not a runtime traffic measurement. It should be stated
as:

> “Based on inspected entrypoints and include chains, the browser application
> appears legacy-dominated.”

The architecture Mermaid diagram should not imply that every `src/` class uses
`QueryUtils`; the source also contains selected DBAL and ORM paths. Edges
should be restricted to verified caller groups.

## Negative findings and their limits

Statements such as the following are valid only with an inspection-scope
qualifier:

- No direct `billing` table access was found in the inspected Scheduling paths.
- No automatic name/DOB duplicate check was found in the inspected patient-save
  path.
- No complete transaction/rollback boundary was found around the inspected
  registration sequence.
- No direct core audit/notification write was found in the inspected
  registration path.

They should not be read as repository-wide proofs. The precise wording should
be:

> “Not found in the inspected files and direct call paths; not a repository-wide
> absence claim.”

## Claims requiring additional downstream tracing

### Contact and address persistence

The patient registration document correctly stops short of naming exact
contact/address tables. Completing that trail would require following
[`ContactService.php`](../src/Services/ContactService.php) and
[`ContactAddressService.php`](../src/Services/ContactAddressService.php).

### Event/module side effects

The registration document correctly identifies event dispatch and listener
points but does not prove every downstream write. Module-specific effects
should remain conditional:

> “If the module is installed and subscribed, the event may trigger
> module-specific side effects.”

### Defined validation rules not invoked by the path

The distinction in the registration document between
[`PatientValidator.php`](../src/Validators/PatientValidator.php) and the
comprehensive save path is source-supported and should be retained. Defined
validator rules must not be presented as enforced registration behavior unless
the call path reaches the validator.

## Recommended documentation corrections

1. Replace the nonexistent `portal/app.php` citation.
2. Add `openemr_postcalendar_events` and
   `openemr_postcalendar_categories` to the Scheduling context inventory.
3. Normalize API route placeholders and distinguish numeric IDs from UUIDs.
4. Label bounded-context ownership as an analytical classification.
5. Narrow architecture-diagram database edges to verified caller paths.
6. Add the inspection-scope qualifier to negative findings.
7. Keep contact/address, event subscriber, transaction-boundary, and duplicate
   detection limitations explicit.
8. Add line-level citations for key route and behavior claims when documents
   are revised.

## What changed

- Created this audit report at
  [`legacy-diagrams/docs-legacy-audit.md`](./docs-legacy-audit.md).
- No files under `docs/legacy/` were changed because that directory does not
  exist.
- No existing source files or prior documentation files were modified during
  this audit.
