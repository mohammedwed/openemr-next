# 0052. The Firely .NET SDK replaces the hand-rolled FHIR Patient mapping, and adds US Core validation

- **Status:** Accepted
- **Date:** 2026-11-14
- **Builds on:** [0051](0051-m4-scope-expands-to-full-legacy-parity-minus-portal.md) (the FHIR Patient API slice this replaces the implementation of)

## Context

The FHIR Patient API slice (ADR 0051, 4/4) originally shipped with a hand-rolled FHIR R4 `Patient`
resource shape — a plain C# class tree serialized by hand, matching legacy's own PHP approach (no FHIR
library there either). That shipped and merged (PR #59). Separately, the user wanted to evaluate
[Firely's .NET SDK](https://github.com/FirelyTeam/firely-net-sdk) as a portfolio-relevant skill: real
FHIR model types, conformance-based validation against a named profile (US Core), rather than a
resource shape this project invented and validates against nothing.

## Decision

Replace the hand-rolled `FhirPatientResource`/`CreateFhirPatientRequest` types with the real
`Hl7.Fhir.Model.Patient` from Firely's SDK, and add a `POST /fhir/Patient/$validate` endpoint backed by
real US Core 8.0.0 conformance checking.

**Package choice, arrived at empirically, not from documentation claims:**

- `Hl7.Fhir.R4` pinned to **5.11.1**, not the newer 6.x line. The 6.x generation splits the model
  package into `Hl7.Fhir.Base` + `Hl7.Fhir.Conformance` + `Hl7.Fhir.R4`; the simple, documented
  validator (`Hl7.Fhir.Validation.Legacy.R4`) is hard-pinned to `Hl7.Fhir.R4 5.11.1` and throws
  `TypeLoadException: IBaseElementNavigator<T> not found` against 6.x — confirmed by actually running
  it, not inferred from release notes. The 6.x-generation validator
  (`Firely.Fhir.Validation.R4`) has no equivalent simple top-level API; it only exposes low-level
  `SchemaBuilder`/`ElementSchema` types.
- `Firely.Fhir.Packages` (live package-registry resolution, e.g. pulling `hl7.fhir.us.core` from
  `packages.fhir.org` at runtime) was tried and abandoned: it's built against the 6.x generation only,
  and referencing it alongside `Hl7.Fhir.R4 5.11.1` produces `CS0433` duplicate-type errors
  (`HumanName` exists in both `Hl7.Fhir.Base` and `Hl7.Fhir.R4`) — the two generations cannot be mixed
  in one project at all, confirmed hands-on with a throwaway sandbox project before deciding.
- Instead, the two FHIR packages this needs (`hl7.fhir.r4.core` and `hl7.fhir.us.core@8.0.0`) are
  **vendored as `.tgz` files** under `FhirProfiles/`, downloaded once from packages.fhir.org and
  extracted in-process at startup (`System.Formats.Tar.TarFile` + `GZipStream` — no extra NuGet
  dependency needed for that step) into a `CommonDirectorySource` the legacy validator reads from.
  Offline, deterministic, no runtime dependency on a package registry being reachable.

**Mapping fixes found along the way, not introduced by the rewrite:**

- The original hand-rolled mapper used `ValueCoding` (a bare `Coding`) for the gender-identity and
  tribal-affiliation extensions. Re-checked against legacy PHP's own `setValueCodeableConcept()` vs
  `setValueCoding()` calls: both of those two extensions use `valueCodeableConcept` (a nested
  `CodeableConcept`), not a bare coding. Fixed in the rewrite — a real, previously-shipped bug, not a
  new finding about the library.
- `ResolveGenderIdentityAsync`'s explicit `"UNK"` branch incorrectly reported `Known = false`; only a
  genuinely blank source value should omit the extension, and an explicit "UNK" is not blank. Fixed; see
  `docs/m4-domain-notes.md` for the full before/after.

## Consequences

- `OpenEmrNext.PatientManagement.Api.csproj` now depends on `Hl7.Fhir.R4 5.11.1` and
  `Hl7.Fhir.Validation.Legacy.R4 5.11.0`, plus the two vendored `.tgz` profile packages shipped as
  content files (`CopyToOutputDirectory`).
- `FhirPatientMapper` now builds real `Hl7.Fhir.Model.Patient` objects instead of the hand-rolled
  resource tree; `FhirJson` wraps the 5.x generation's string-based serializer (no native
  `System.Text.Json` converter exists in this generation).
- A future upgrade to the 6.x generation is a real migration, not a version bump — it would need either
  a working simple validator API to appear for that generation, or a rewrite onto the low-level
  `SchemaBuilder` API. Not attempted here; flagged for whoever picks this up next.
- `$validate` is additive to the FHIR Patient API's existing `GET`/`POST` surface from ADR 0051 — no
  change to that surface's own scope decisions (no `PUT`, no `$export`, no `DELETE`).

## Alternatives considered

- **Keep the hand-rolled shape, skip a FHIR library entirely.** Matches legacy's own approach and is
  valid parity-wise, but forecloses any real conformance validation and the portfolio value of
  demonstrating a real FHIR SDK integration — rejected per the user's explicit preference.
- **Adopt the 6.x generation outright, accepting the validator gap.** Rejected: there is no documented,
  simple top-level validation API for that generation today; building one from `SchemaBuilder` directly
  is a much larger, separate effort than this slice's scope.

## Related decisions

Builds on [0051](0051-m4-scope-expands-to-full-legacy-parity-minus-portal.md), whose FHIR Patient API
slice this changes the implementation of (not its scope — read/search/create/validate, no write-path
expansion).
