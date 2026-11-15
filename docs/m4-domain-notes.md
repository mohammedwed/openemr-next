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

## FHIR Patient API: read is real, write is legacy's own admitted gap

Grounded in `FhirPatientService.php` (1080 lines) and `FhirPatientRestController.php` while building
the fourth and final slice of the full-legacy-parity plan.

- **Legacy's own code admits its write path is incomplete.** A comment directly in
  `parseFhirResource()`: *"ONC certification only deals with READ operations, the mapping of FHIR
  values such as language, ethnicity etc are NOT being done here and so the creation/updating of
  resources is currently NOT correct."* Race, ethnicity, and language are read on output but never
  mapped back on input (ethnicity/race's reverse-mapping calls are literally commented out in the
  source). This service's own create path matches that same gap deliberately, not as a new reduction.
- **`PUT` (update) is deferred, not built.** The plan's own assumption — "create/update already reuse
  the same `PatientService::insert()`/`update()` the plain registration path uses" — turned out to be
  wrong for *this* service: only `RegisterPatientUseCase`/`IPatientRegistry.RegisterAsync` exist here
  (create-only). Legacy's `update()` has no equivalent yet, so building `PUT` would mean a genuinely
  new `Patient.Update()`/persistence capability, not reusing what exists. Given legacy's own `PUT` is
  also one of the admittedly-incorrect write paths above, `GET` (read + search) and `POST` (create)
  were the ones worth building first.
- **Three coding systems resolve through `list_options`, differently.** Race/ethnicity store their
  code directly in `list_options.notes` (no `:` separator, a fixed system constant per list). Every
  other coded extension (administrative sex, gender identity, tribal affiliation, interpreter-needed,
  language) stores `SYSTEM:CODE` in `list_options.codes` (`CodeTypesService::parseCode()`), with only
  two code types actually seeded anywhere: `SNOMED-CT` and `DataAbsentReason`.
- **"Omitted" and "added with an Unknown/absent default" are two different legacy behaviors, not
  interchangeable.** Race, administrative sex, and interpreter-needed always add their extension
  (defaulting to `UNK`/`unknown` when blank). Ethnicity, gender identity, and tribal affiliation are
  *entirely absent* from the resource when their source column is blank — a real distinction, confirmed
  against each function's own `if (!empty(...))` guard (or its absence), not inferred from behavior.
- **A real, previously-latent gap surfaced and got fixed along the way**: `patient_data.date` (the
  table's own last-updated timestamp, see the naming trap above) was never actually written by
  `Patient.Register()` — nothing had read it back yet to notice. The FHIR mapping is the first consumer
  (as `last_updated`), which is what surfaced it; `Patient` now sets it at registration, the same moment
  as `regdate`, matching legacy's own `databaseInsert()`.
- **Scope reductions, decided deliberately:** a single target US Core profile version (8.0.0 — legacy
  branches across three; this service has no installed base to stay compatible with). Previous-names
  history (`patient_history`) and additional/historical addresses (`patient_additional_addresses`) are
  not mapped — only the current name and current address, satisfying US Core's "must support one name,
  one address" bar. Search supports the same ~15 parameters as exact-or-contains matches combined with
  AND, not legacy's own search modifiers (`:missing`, `:exact`, date comparators). `$export`, `DELETE`,
  `$everything`, and `$match` are out of scope (the first per the plan; the rest because legacy doesn't
  implement them either).

## Firely .NET SDK: a real FHIR model and validator, grounded empirically not from docs (ADR 0052)

The hand-rolled `Patient` resource shape above was replaced with the real `Hl7.Fhir.Model.Patient` from
Firely's SDK, plus `$validate` against real US Core 8.0.0 conformance. See ADR 0052 for the decision;
these are the implementation findings underneath it.

- **The SDK has two incompatible generations, and documentation alone did not reveal that.** `Hl7.Fhir.R4`
  6.x splits into `Hl7.Fhir.Base`/`Hl7.Fhir.Conformance`/`Hl7.Fhir.R4`; 5.x is self-contained. The simple,
  documented validator (`Hl7.Fhir.Validation.Legacy.R4`) only works against 5.x — confirmed by actually
  running it against 6.x and getting `TypeLoadException: IBaseElementNavigator<T> not found`, not by
  reading a compatibility matrix. `Firely.Fhir.Packages` (live package-registry resolution) is 6.x-only
  and produces `CS0433` duplicate-type errors (`HumanName` defined twice) the instant it's referenced
  alongside `Hl7.Fhir.R4 5.11.1` in the same project — the two generations cannot coexist at all.
- **Live package resolution was abandoned for local `.tgz` extraction.** Rather than fetching
  `hl7.fhir.us.core` from `packages.fhir.org` at runtime (what `Firely.Fhir.Packages` is for, but
  version-incompatible here), the two needed packages are vendored under `FhirProfiles/` and extracted
  in-process at startup via `System.Formats.Tar.TarFile` + `GZipStream` into a `CommonDirectorySource` —
  no extra NuGet dependency, fully offline, deterministic across environments.
- **`FhirPackageSource`'s actual namespace was found by reflection, not by guessing from the package
  name.** It lives in `Firely.Fhir.Packages`, not `Hl7.Fhir.Specification.Source` as the initial research
  suggested — resolved with a small throwaway program dumping the assembly's actual exported types.
- **Record `with`-expressions and validation don't mix unless every property is read-only.** Confirmed
  hands-on (a throwaway sandbox project, not just reasoning about C# semantics): a record property with
  only `{ get; }` makes `instance with { Prop = x }` a compile error (`CS0200`); `with { }` (an untouched
  copy) still compiles. This is the actual mechanism (not just a convention) behind
  `InvariantEnforcementTests` below.

## Domain layer structural hardening: ValueObjects/Rules/ReadModels, and an architecture test for it

Prompted by an explicit request to audit the whole Domain layer for missing validation and unclear
structure, independent of any single feature slice.

- **Every DDD role now has its own folder, per subdomain**: `ValueObjects/`, `Rules/` (static pure-function
  classes like `EffectiveInsuranceRule`, `PatientGenderMapping`, `DuplicateScore`), and `ReadModels/`
  (`PatientFhirView`, `InsurancePolicyRow`, `EffectiveInsurancePolicy`, `AdvanceDirectiveDocumentSummary`),
  mirrored in the test project. Previously these sat flat alongside the aggregates with no structural
  signal for which role each type played.
- **`ResolvedCode` (a new value object for FHIR's `system`/`code`/`display` triples) and
  `PatientRegistration` both moved to validate-on-construction**: private constructor + a `Create()`
  factory that collects every error before throwing, rather than trusting a caller to have already
  checked. `PatientRegistration`'s validation used to live inline in `Patient.Register()` — moved out so
  the value object enforces its own invariant regardless of caller.
- **`Patient.Register()` gained a `registeredBy` check** (must be a positive id) — found during the same
  audit: every other required field was validated, this one wasn't.
- **`InvariantEnforcementTests`** (new architecture test): reflection-based, asserts that `Patient`,
  `InsurancePolicy`, `AdvanceDirectiveDocument`, `PatientRegistration`, and `ResolvedCode` have no public
  constructor and no publicly-settable property. This is what actually keeps the validate-on-construction
  pattern from regressing — a future change that quietly adds a public setter "to make testing easier"
  fails this test instead of silently reopening the gap the `with`-expression finding above describes.

## InsuranceCoverage: the first multi-entity aggregate cluster (ADR 0053)

A self-review of the Domain layer (see below) scored DDD design well but flagged that every aggregate
until now was single-entity — no invariant had ever been enforced *across* a cluster of child entities.
Legacy's own `InsuranceService::swapInsurance()` — moving one policy to a level, trading places with
whatever already occupies it — is a genuine two-entity operation, not an invented example. ADR 0053
records the full decision; in short: `InsuranceCoverage` now owns the swap decision and a cluster-wide
uniqueness invariant (no two policies at the same level and start date), `InsurancePolicy.Level` became
mutable through a narrowly `internal` `ChangeLevel`, and the old single-method `SwapAsync` port was split
into `GetPoliciesAsync`/`PersistSwapAsync` so the decision logic is unit-testable without a database.

## Architecture self-assessment: all four tracked dimensions closed out

Recorded 2026-11-14, when asked to rate the Domain layer out of 10 on clean architecture, documentation,
security/invariant-enforcement, DDD, and SOLID. All five items the rating flagged are now resolved or
deliberately accepted, in the order addressed:

- **Security/invariant enforcement (was 7/10) — resolved.** `AdvanceDirectiveDocument.Upload` now
  enforces `MaxContentBytes` (30 MiB, matching legacy's own production `upload_max_filesize`/
  `post_max_size`, `docker/release/php.ini`) — nothing can construct a document with an arbitrarily
  large payload in memory anymore.
- **Clean Architecture (was 8/10) — resolved.** `ResolvedCode`'s fields were renamed
  `System`/`Code`/`Display` → `CodeSystem`/`CodeValue`/`DisplayText`, and `PatientFhirView.EthnicityOmb`
  → `EthnicityCategory` — Domain no longer echoes FHIR's own wire vocabulary (`Coding.system/code/display`,
  the `ombCategory` extension field) by name. `FhirPatientMapper.ToCoding` (Api layer) is now the one
  place those Domain-native names become an actual FHIR `Coding`.
- **DDD — read-model boundary (was 8/10) — resolved.** Every read model, plus `EffectiveInsuranceRule`
  (the one Domain rule that operates purely on read models), moved into a new project,
  `OpenEmrNext.PatientManagement.Domain.ReadModels` (ADR 0054) — a real, compiler-enforced boundary, not
  just a folder. Namespaces are unchanged; only the physical project moved. (The single-entity-aggregate
  half of the original DDD score was already resolved by `InsuranceCoverage`, ADR 0053.)
- **SOLID — Open/Closed (was 8/10) — reviewed, left as-is.** The enums' `switch`-based extension methods
  (`InsuranceLevel`, `AdvanceDirectiveType`, etc.) still need touching for every new value. Considered and
  deliberately not refactored: these are small, legacy-grounded, effectively closed sets (OpenEMR's own
  schema fixes them at 3 values each), and any more "open" pattern (a registry, a class hierarchy) would
  add real indirection to remove a theoretical violation that never actually bites in practice. Confirmed
  with the user rather than silently left alone or silently "fixed" for its own sake.
- **Documentation — resolved** for the whole segment this rating covered, across this entry and ADRs
  0052/0053/0054.

## Application/Application.Ports self-assessment: one bug and three shortcomings, all closed

Recorded 2026-11-14, from the same kind of grounded, evidence-based pass as the Domain layer
self-assessment above, this time over all 11 use cases in `Application` and all 7 ports in
`Application.Ports`.

**Fixed**: `AddInsurancePolicyUseCase` called `InsurancePolicy.Add(...)` directly instead of going
through `InsuranceCoverage.AddPolicy` — the cluster-wide "no two policies at the same level and start
date" invariant `InsuranceCoverage` exists to enforce (ADR 0053) was only ever reachable through
`SwapInsuranceUseCase`, never through plain add, and nothing exercised this in any test. The
`(pid, type, date)` SQL unique constraint backstopped the actual data integrity, so this was never
silent corruption — but a caller hit a raw, unhandled database exception instead of a clean
`InsurancePolicyValidationException`. Fixed by routing `AddInsurancePolicyUseCase` through
`InsuranceCoverage.For`/`.AddPolicy`, the same shape `SwapInsuranceUseCase` already uses.

That fix surfaced a second, real latent bug in `EfInsurancePolicyRepository.GetPoliciesAsync`: it read
`date`/`date_end` as typed `DateTime` columns directly, which throws when a pre-existing row (written
before this service existed, or by legacy's own PHP) holds a zero or partial date — previously "safe"
only because `GetPoliciesAsync` was only ever called from the swap path, where ADR 0053 reasoned every
row touched was one this service itself created. Routing *add* through the same method made that
reasoning false: an unrelated malformed row anywhere on a patient's record would now break every new
insurance add for that patient. Fixed by reading dates as `CAST(... AS CHAR(10))` strings (the same
pattern `GetRowsAsync` already uses) and skipping, not throwing on, a row whose type or date doesn't
parse — losing nothing a caller could observe, since such a row could never collide with a new policy's
real date anyway. Covered by two new integration tests against a real Testcontainers MariaDB:
duplicate-policy rejection, and tolerance of an unrelated pre-existing zero-date row.

**Also fixed, in order (2026-11-14)**:

- **Clean Architecture**: `IAdvanceDirectiveFileStore` moved from `Application.Ports` into
  `Infrastructure`, where its only two consumers (`EfAdvanceDirectiveRepository`,
  `LocalDiskAdvanceDirectiveFileStore`) already lived — nothing in `Application` ever referenced it,
  so it was never a true hexagonal port, just an internal adapter-to-adapter collaboration wearing a
  port's naming convention. The composition root's DI wiring (`PatientManagementApiExtensions`) needed
  no change: it already imported `Infrastructure`'s namespace.
- **Documentation**: all 11 `Application` use cases now have a real XML doc comment explaining *why*,
  matching `Application.Ports`' existing 7/7 — closing the asymmetry the self-assessment flagged.
- **SOLID**: removed the 6 dead `using` statements (`GetPatientFhirRecordUseCase.cs`,
  `SearchPatientFhirRecordsUseCase.cs`, `IPatientFhirRepository.cs` each importing the unused, now-empty
  `OpenEmrNext.PatientManagement.Domain.Fhir` and `...Fhir.Rules` namespaces).

**Tracked separately, not fixed — a test-quality issue found along the way, not one of the five
review dimensions**:

- **`DuplicateScoringTests` is flaky via a SOUNDEX collision across tests, not just this test
  file's own assumption.** Found when PR #64's first CI run failed
  `A_new_patient_with_no_one_to_compare_against_scores_zero` (expected score `0`, got `2`) on a run
  that touched nothing in duplicate-scoring code at all; a re-run of the same commit passed clean,
  confirming it's pre-existing flakiness, not a regression. Root cause: the test's own comment claims
  "every field here (sex included) is unique to this test," but the "Solo" patient's last name is
  `Patient{Guid.NewGuid():N}` — MySQL's `SOUNDEX()` only encodes a short prefix of the string, so two
  different tests' GUID-suffixed last names that both start with `"Patient"` collapse to the *same*
  soundex code and score 2 points against each other, even though the full strings are unique. Given
  `DuplicateScoringTests`' own fixture doc comment ("the database is shared across every test in this
  collection, not reset between them"), this can trigger whenever another concurrently-run test's
  last name happens to share enough of a soundex-equivalent prefix -- not deterministic, so it won't
  reproduce on every run. Not fixed here (out of scope for the Application-layer PR that surfaced it);
  the real fix is almost certainly giving each test's "unique" demographic fields a prefix that doesn't
  collide under `SOUNDEX()`, e.g. randomizing the leading letters too, not just appending a GUID suffix.

## Infrastructure self-assessment: all four findings addressed

Recorded 2026-11-14, same grounded pass over all 10 files in `Infrastructure`. Security and
Documentation came back strong from the start (every raw SQL call is properly parameterized --
confirmed by reading every call site, not just the obviously risky ones; every file has a real doc
comment grounded in the actual legacy source). Also prompted a side discussion on whether to replace
raw SQL with EF Core here -- decided against a rewrite: the zero/partial-date tolerance every
repository exists to provide has no clean EF-native equivalent (a `ValueConverter` can't help, since
the MySQL driver throws before it would ever run), and the legacy-specific SQL (`SOUNDEX()`, the
`sequences` global counter, the sentinel-date swap workaround) would stay raw regardless, making a
"full EF" rewrite a hybrid, not a clean win, for real migration risk.

**Fixed**:

- **Performance -- N+1 queries in `EfPatientFhirRepository.SearchAsync`.** `BuildViewAsync` ran up to
  7 separate `list_options` lookups per patient (race, ethnicity, administrative sex, gender identity,
  tribal affiliation, interpreter-needed, language), each its own round trip, called once per row in
  the search loop -- a 50-patient search could issue ~350 queries. Fixed within the same raw-SQL
  approach, no EF needed: `LoadListOptionsAsync` now loads every row for the 7 lists this mapping
  touches in one query per `GetByUuidAsync`/`SearchAsync` call (`list_options` is bounded configuration
  data, not scaled by patient count), and every resolver became a synchronous in-memory dictionary
  lookup instead of its own round trip.
- **Security (defense-in-depth) -- `LocalDiskAdvanceDirectiveFileStore.ReadAsync`.** Now resolves the
  full path and rejects (throwing `UnauthorizedAccessException`) anything that lands outside
  `repositoryRoot`, covering both a `file://`-prefixed and a bare traversal attempt. Covered by new
  unit tests (`LocalDiskAdvanceDirectiveFileStoreTests`) that don't need Testcontainers at all --
  `LocalDiskAdvanceDirectiveFileStore` has no DB dependency.
- **SOLID -- duplicated transaction/`LAST_INSERT_ID()` boilerplate.** Extracted into
  `DatabaseFacadeExtensions.GetLastInsertIdAsync`, a single shared extension method (with the
  connection-pinning explanation that used to be duplicated in both doc comments) now used by both
  `EfInsurancePolicyRepository.AddAsync` and `EfAdvanceDirectiveRepository.AddDocumentAsync`.

- **DDD/consistency -- `EfAdvanceDirectiveRepository.AddDocumentAsync` isn't atomic across disk and
  database.** Still writes the file first, then opens the DB transaction (the safer of the two naive
  orderings), but now wraps the DB portion in try/catch: on any failure, it best-effort deletes the
  file it already wrote via a new `IAdvanceDirectiveFileStore.DeleteAsync`
  (`LocalDiskAdvanceDirectiveFileStore`'s implementation reuses the same path-containment check as
  `ReadAsync`), then rethrows -- a cleanup attempt that fails must never replace or hide the real
  exception. **Deliberately partial, not a full fix**: this only compensates for a *handled* exception
  during the DB write; a process killed between the file write and the DB commit still leaves an
  orphan behind, which would need a real background reconciliation job, out of scope for this round.
  **Test-coverage gap, stated plainly**: the compensating-delete path itself (the `catch` block
  actually firing) has no automated test -- forcing a genuine database failure through the public API
  isn't straightforward here, since both unique-constrained columns that could trigger one
  (`documents.uuid`, `documents.drive_uuid`) are generated inside `AdvanceDirectiveDocument`'s own
  constructor, not settable by a caller. `DeleteAsync` itself (the new, reusable piece) is covered by
  direct unit tests (`LocalDiskAdvanceDirectiveFileStoreTests`) that don't need a database at all.

## A new review dimension: Resilience and failing fast

Added 2026-11-14 alongside the five already in use (Clean Architecture, Documentation, Security, DDD,
SOLID). Resilience only really applies where I/O happens (`Infrastructure`, and startup configuration
in `Composition`/`Api`) -- `Domain` has none by design (`LayerDependencyTests`), so for `Domain` this
dimension only ever measures "failing fast," not resilience. Two real findings; one fixed, one tracked:

- **Fixed -- no transient-fault retry on the database connection (ADR 0056).**
  `PatientManagementApiExtensions.AddPatientManagementHttpDependencies` called
  `options.UseMySql(connectionString, ...)` with no `EnableRetryOnFailure()` -- a brief network blip to
  MariaDB failed every in-flight request outright, no automatic recovery. For comparison: AuthFacade has
  a dedicated `Resilience/` folder (`OpenEmrCircuitBreakers`, `CircuitBreakerPipeline`, built on Polly)
  guarding its own external dependency (OpenEMR's HTTP API); PatientManagement's external dependency --
  the database -- had no equivalent. **Not a one-line fix**: this project's three explicit transactions
  (`EfInsurancePolicyRepository.AddAsync`/`PersistSwapAsync`, `EfAdvanceDirectiveRepository.AddDocumentAsync`)
  all needed wrapping in `Database.CreateExecutionStrategy().ExecuteAsync(...)` -- turning on
  `EnableRetryOnFailure()` without that throws `InvalidOperationException` the first time a retry
  actually fires, not at startup. `AddAsync` and `AddDocumentAsync` also needed
  `AssignGeneratedIdentity` moved outside the retried delegate, returning the generated id instead and
  assigning it exactly once after a successful commit -- otherwise a retried attempt (which
  re-generates a fresh id from scratch) would call it twice on the same aggregate and throw.
  `PersistSwapAsync` needed only the wrapping, since it mutates no external object as a side effect of
  running. Verified against a real Testcontainers MariaDB: all 46 integration tests pass, including
  every transactional path -- a wrong wrapping would have thrown immediately on any transaction use,
  not only during an actual retry, so this is real confirmation, not just "it compiled."
- **Fixed -- no global exception-handling middleware anywhere in PatientManagement's Api.** Confirmed
  this was a repo-wide gap, not specific to this service -- `AuthFacade` and `Gateway` still don't have
  `UseExceptionHandler`/`ProblemDetails` either, only this service does now. Every endpoint already
  caught its own known validation exceptions inline, consistently; anything unanticipated (a transient
  DB error that exhausted ADR 0056's retries, a real bug) previously fell through to ASP.NET's bare
  default 500. Fixed with the minimal built-in pattern -- `builder.Services.AddProblemDetails()`
  (`PatientManagementApiExtensions`) plus `app.UseExceptionHandler()` (`Program.cs`), no custom
  `IExceptionHandler` needed: ASP.NET Core's own exception middleware already logs the exception at
  Error level and writes a `ProblemDetails` body for the current status code once `IProblemDetailsService`
  is registered. Verified against the real HTTP pipeline, not just by reasoning about the framework
  API: a new test (`GlobalExceptionHandlingTests`) swaps in an always-throwing `IPatientFhirRepository`
  via `ConfigureTestServices` and confirms `GET /fhir/Patient/{uuid}` now returns a 500 with an
  `application/problem+json` body, not a bare empty response.

**Already a genuine strength, confirmed not assumed**: startup configuration fails fast correctly --
`[Required]` DataAnnotations plus `.ValidateOnStart()` on both `PatientManagementDbOptions` and
`AdvanceDirectiveStorageOptions` (`CompositionServiceCollectionExtensions`) throw
`OptionsValidationException` at startup, the same pattern already seen crashing a sibling service's
startup in a CI log earlier this session -- not a silent empty-string default reaching production.

## Api self-assessment: three findings, all fixed

Recorded 2026-11-15, the same grounded pass over all 19 files in `Api` (endpoints, contracts,
`FhirJson`, `FhirProfileValidator`, `Program.cs`, composition). Resilience & failing fast scored
strongly here by construction (ADRs 0056/0057 live in this project); DDD and Clean Architecture were
already solid (every endpoint delegates to Application/Domain, never hand-rolls validation). Three
real findings, all fixed:

- **Documentation -- an inconsistently-applied caveat.** `RegisterPatientRequest.RegisteredBy`
  documented "caller-supplied for now, not verified against any authenticated session -- an
  already-accepted gap inherited from ADR 0048" -- but the identical pattern on
  `UploadAdvanceDirectiveDocumentRequest.OwnerId`, `SetAdvanceDirectiveStatusRequest.AuthenticatorUserId`,
  and `FhirPatientEndpoints.CreatePatient`'s `registeredBy` query parameter carried no such note. Fixed
  by adding the same caveat to all three. This is **not a new security finding** -- the lack of
  authentication itself is ADR 0048's own explicit, named deferral (no token exists to validate until
  the gateway routes a second service through it) -- only the missing documentation was new.
- **SOLID -- duplicated registration orchestration.** `PatientEndpoints.RegisterPatient` and
  `FhirPatientEndpoints.CreatePatient` each independently called
  `PatientRegistration.Create(...)` then `RegisterPatientUseCase.ExecuteAsync(...)` -- identical
  construct-then-register sequence duplicated across two Api files for two different input shapes (raw
  HTTP fields vs. FHIR-resource-extracted fields). Fixed by adding a convenience overload to
  `RegisterPatientUseCase.ExecuteAsync` that accepts the raw primitive fields and constructs the
  registration internally -- additive, not a breaking change to the existing
  `PatientRegistration`-accepting overload several integration tests already use directly to test
  registration persistence given an already-valid registration. Each endpoint still shapes its own
  error response for `PatientValidationException` (REST `ValidationProblem` vs. FHIR
  `OperationOutcome`) -- only the construction call itself stopped being duplicated.
- **Security (defense-in-depth) -- a predictable, unversioned temp-cache path.**
  `FhirProfileValidator.ExtractIfNeeded` extracted into a fixed `Path.GetTempPath()` subdirectory,
  gated only by a marker file's *existence*, not the source `.tgz`'s content -- a staleness risk if
  `/tmp` ever persisted across a redeploy with updated profile packages, and (low real-world severity
  given this runs in an isolated container) a theoretical plant-a-fake-marker risk on a
  non-containerized, multi-tenant host. Fixed by making the cache content-addressed: the destination
  subdirectory and the marker file's own content are both keyed on the source `.tgz`'s SHA-256 hash, so
  a changed package version naturally invalidates the old cache entry instead of silently reusing it,
  and skipping extraction now requires matching that exact hash, not just a marker file's presence.
- Also found and fixed, while already in `FhirPatientEndpoints.cs`: 2 more dead `using` statements
  (`OpenEmrNext.PatientManagement.Domain.Fhir` and `...Fhir.Rules`) that the earlier
  Application/Application.Ports SOLID cleanup round missed in this one file.

## Api follow-up: request validation and a cleanup pass

Recorded 2026-11-15, after the three findings above. Two more rounds of work on `Api`, both
user-initiated rather than found during the six-dimension pass itself:

- **DataAnnotations added to every Request DTO.** All 5 `Contracts/Requests` types
  (`RegisterPatientRequest`, `AddInsurancePolicyRequest`, `SwapInsuranceRequest`,
  `SetAdvanceDirectiveStatusRequest`, `UploadAdvanceDirectiveDocumentRequest`) now carry
  `[Required]`/`[StringLength]`/`[Range]`/`[EmailAddress]` attributes, enforced by
  `builder.Services.AddValidation()` (`PatientManagementApiExtensions.cs`) -- ASP.NET Core 10's
  built-in minimal-API validation, wired globally via `Microsoft.AspNetCore.Routing`'s
  `ValidationEndpointFilterFactory`, not a hand-written filter. Every `ErrorMessage` matches the
  corresponding Domain factory's own exact wording wherever an equivalent Domain rule exists (e.g.
  `PatientRegistration`'s "First name must be between 1 and 255 characters."), so a caller sees
  identical text whichever layer actually caught the problem; a handful of fields with no Domain
  counterpart (`AuthenticatorUserId`, and the string-encoded enums `Level`/`Status`/`Type`/
  `TargetLevel`) got new, purpose-written messages instead. This is restating Domain's validation
  earlier, not replacing it -- the Domain factories remain the actual source of truth and still run
  on every request regardless of what the attributes catch first. The one genuine prior gap this
  closes: `AuthenticatorUserId` had no validation anywhere before this (neither
  `SetAdvanceDirectiveStatusUseCase` nor `IAdvanceDirectiveRepository` checked it), now
  `[Range(1, long.MaxValue)]`. Covered by a new integration test,
  `PUT_status_with_a_non_positive_authenticator_id_returns_400`.
- **Code-cleanliness pass.** A new `ValidationProblems` helper
  (`Endpoints/ValidationProblems.cs`) centralizes the `Results.ValidationProblem(new
  Dictionary<string, string[]> { ["errors"] = ... })` construction that was duplicated 8 times
  across `PatientEndpoints`, `InsuranceEndpoints`, and `AdvanceDirectiveEndpoints` -- `Single(...)`
  for one error, `Many(...)` for a list. `FhirPatientMapper`'s own private `UsCoreProfile` constant
  (an independent second copy of the same profile URL already defined as
  `FhirProfileValidator.UsCorePatientProfile`) was removed in favor of the existing one. 4 more dead
  `using` statements removed (`Domain.Insurance.Rules`, `Domain.Insurance.ReadModels` in
  `InsuranceEndpoints.cs`; `Domain.AdvanceDirectives.ReadModels` in `AdvanceDirectiveEndpoints.cs`;
  a bare `Domain` in `FhirPatientEndpoints.cs`, verified safe to remove without reintroducing the
  `Patient`/`FhirPatient` ambiguity an earlier fix deliberately avoided) -- found by removing each
  suspected one and doing a full rebuild, not by static inspection alone.

Build-verified only; the integration test suite has not been run against either round yet.

## Hardcoded-values audit (PatientManagement)

Recorded 2026-11-15. Swept all six `PatientManagement` projects (`Domain`, `Application`,
`Application.Ports`, `Infrastructure`, `Composition`, `Api`) for literals that should arguably be
configuration instead. Everything found was already either a bound, overridable options default
(`PatientManagementDbOptions.Port = 3306`; `AdvanceDirectiveStorageOptions.RepositoryRoot` resolved
from config, not hardcoded, in `LocalDiskAdvanceDirectiveFileStore`) or a legacy-grounded Domain
invariant with its source already cited in a comment (`AdvanceDirectiveDocument.MaxContentBytes` ==
`php.ini`'s 30M; `DuplicateScore.DisplayThreshold`/`HighlightThreshold` == the legacy scoring
formula's own 12/17; the `255`/`30`-ish string-length bounds across `PatientRegistration` and
`InsurancePolicy` == legacy's own column widths; the `9999-12-31` open-ended-policy sentinel; the
FHIR/LOINC/RFC spec URIs in `FhirPatientMapper`/`EfPatientFhirRepository`/`Rfc4122GuidConverter`) --
none of these are new findings.

One real finding: `new MariaDbServerVersion(new Version(12, 3, 3))` is hardcoded identically in
**three** places repo-wide -- `PatientManagementApiExtensions.cs` (production), the test fixture
`PatientDataContainerFixture.cs`, and (out of this audit's scope, but the same literal)
AuthFacade's own `OpenIddictClientServiceCollectionExtensions.cs`. `PatientManagementApiExtensions.cs`'s
own comment already acknowledges this as deliberate ("same pin AuthFacade's own DbContext already
uses"), so it's a known, repo-wide convention, not an oversight -- but the two `PatientManagement`
copies (prod + test) aren't sharing a single source of truth within this service. Left as-is pending
a decision: either leave it (each of the two copies is small and already commented), or deduplicate
the two `PatientManagement` copies behind one shared `internal const` (not touching AuthFacade, which
is a separate deployable service entitled to pin its own MariaDB client version independently).

## PatientManagement.Api now requires a shared internal-gateway secret

Recorded 2026-11-15 (ADR 0055). PatientManagement.Api had no authentication or authorization at all --
every endpoint trusted caller-supplied identity fields unconditionally. The original goal was to close
this properly: gate the service *and* forward the real authenticated user's identity. Research found the
second half isn't buildable yet -- AuthFacade's session only carries OpenEMR's UUID `sub` claim, with no
claim or lookup anywhere in this codebase that resolves it to the numeric `users.id` that
`RegisteredBy`/`OwnerId`/`AuthenticatorUserId` actually need (they map onto real legacy integer FK
columns -- `patient_data.created_by`, `documents.owner`,
`patient_data.advance_directive_user_authenticator` -- confirmed by reading
`PatientManagementDbContext`/`EfAdvanceDirectiveRepository`, neither of which overrides the column type).

Scaled back to the grounded part: every endpoint now requires a shared secret
(`X-Internal-Gateway-Secret`, `InternalGatewayOptions.Secret`), checked by a new
`InternalGatewayAuthenticationHandler` modeled on the deleted `OpenEmrBearerHandler` (ADR 0035) --
constant-time comparison, same `NoResult()`-vs-`Fail()` shape. `RegisteredBy`/`OwnerId`/
`AuthenticatorUserId` are still caller-supplied exactly as before; that gap is unchanged, not fixed, and
is now explicitly tracked (ADR 0055) rather than silently left as-is. No caller attaches this header yet
-- PatientManagement isn't wired into docker-compose or the Gateway's routes at all; per ADR 0030, the
Gateway is the intended future owner of that wiring, not a direct AuthFacade cluster.

## PatientManagement's OpenAPI document: two real gaps, fixed (ADR 0058)

Recorded 2026-11-15. `AddOpenApi()`/Scalar were already wired, but nobody had checked the actual
generated document (`/openapi/v1.json`) against reality. Fetching it directly (running the app with
placeholder config) surfaced two real gaps, both fixed:

- **No security scheme at all.** Every operation showed `security: null` despite ADR 0055's gate
  requiring the shared secret on every endpoint -- Scalar's "Try it" UI had no way to know to send it.
  Fixed with `PatientManagementOpenApi.ConfigurePatientManagement()`, mirroring AuthFacade's own
  `AuthFacadeOpenApi` pattern exactly: a document transformer declares `X-Internal-Gateway-Secret` as
  an `apiKey`/header scheme, an operation transformer adds it wherever `IAuthorizeData` is present --
  every operation here, unlike AuthFacade where only `/me` needs it.
- **DataAnnotations never reached the schema.** `RegisterPatientRequest`'s generated schema had no
  `minLength`/`maxLength`/`format`/`minimum` despite real `[StringLength]`/`[Range]`/`[EmailAddress]`
  attributes genuinely enforced at runtime by `AddValidation()`. Root cause, confirmed by reflection:
  these DTOs are positional records, so the attributes live on the primary constructor's *parameter*,
  not the auto-generated property `JsonPropertyInfo.AttributeProvider` reflects --
  `JsonPropertyInfo.AssociatedParameter.AttributeProvider` is where they actually are. Fixed with a new
  `ValidationAttributesSchemaTransformer` that checks both.
- **Found along the way**: writing `[Range(1, long.MaxValue)]`'s `Maximum` straight through produced
  `9.223372036854776E+18` in the real document, not `9223372036854775807`. `RangeAttribute` has no
  `(long, long)` constructor, only `(int, int)`/`(double, double)`, so the bound is already a lossy
  double inside the attribute itself before any OpenAPI code sees it -- confirmed by reflecting the
  attribute directly, not assumed. Since every `[Range]` here is a "must be positive" idiom, not a real
  upper bound, the fix omits a known-wrong maximum rather than publish one.
- Covered by a new `OpenApiDocumentTests` against the real generated document: the scheme's shape,
  that literally every operation requires it, and that the schema carries the right constraints (and no
  `maximum` key at all for the `long.MaxValue` sentinel case).

## A typed PatientManagement client, generated by NSwag (ADR 0059)

Recorded 2026-11-15. Reuses the exact pattern ADR 0031 already built for AuthFacade's own client: a
new `OpenEmrNext.PatientManagement.Client` project with an `OpenApiReference` against a committed
snapshot (`OpenAPIs/PatientManagement.json`), regenerated by `scripts/dev/generate-patientmanagement-openapi.sh`
-- builds the Api, runs it briefly on a random loopback port with placeholder config
(`PatientManagementDb:*`/`AdvanceDirectiveStorage:RepositoryRoot`/`InternalGateway:Secret`, all
`[Required]` and never committed), reads `/openapi/v1.json`, strips the run's own random `servers`
entry, writes the file, stops the process. No `/health` endpoint exists here (unlike AuthFacade), so
the script polls the OpenAPI document itself for readiness instead.

Worth generating now specifically because ADR 0058 just made the underlying document actually
correct -- the gateway-secret scheme and the DataAnnotations-derived `minLength`/`maximum`/`format`
constraints all flow straight into the generated `PatientManagementClient`'s request/response types.
Nothing consumes this client yet; that's a separate, later decision.

## Scope note

This is vocabulary and rules, not a formal bounded-context diagram or conceptual class model. Per the
earlier discussion: for one aggregate and one use case, the class shape is expected to emerge from writing
the registration use case itself, informed by the terms and rules above, rather than from a separate
upfront design document.
