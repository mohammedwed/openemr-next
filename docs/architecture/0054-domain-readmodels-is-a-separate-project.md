# 0054. Domain's read models move into their own project, `Domain.ReadModels`

- **Status:** Accepted
- **Date:** 2026-11-14
- **Builds on:** `docs/m4-domain-notes.md`'s architecture self-assessment section

## Context

The architecture self-assessment recorded in `docs/m4-domain-notes.md` scored DDD design well but
flagged that `Domain`'s read models (`PatientFhirView`, `InsurancePolicyRow`,
`EffectiveInsurancePolicy`, `AdvanceDirectiveDocumentSummary`, `PatientFhirSearchCriteria`) sat in the
same project as the write-side aggregates (`Patient`, `InsurancePolicy`, `InsuranceCoverage`,
`AdvanceDirectiveDocument`), with no boundary sharper than a folder name. A stricter CQRS-style split
would separate them more forcefully — enforceable at compile time, not just by convention.

## Decision

A new project, `OpenEmrNext.PatientManagement.Domain.ReadModels`, holds every read model and the one
Domain rule that operates purely on read models (`EffectiveInsuranceRule`, which takes
`InsurancePolicyRow`s and returns `EffectiveInsurancePolicy`s — no aggregate involved on either side).
Everything else (aggregates, value objects, write-side rules like `DuplicateScore` and
`PatientGenderMapping`) stays in `Domain`.

Namespaces are **unchanged** (`OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels`, `.Insurance.ReadModels`,
`.AdvanceDirectives.ReadModels`, and `.Insurance.Rules` for `EffectiveInsuranceRule`) — only the physical
project/assembly moved. The read side still conceptually belongs to Domain's ubiquitous language; what
changed is that the boundary is now a real assembly reference, not just a folder.

**Dependency direction**: `Domain.ReadModels` depends on `Domain` (for the value objects its records
carry, e.g. `PatientFhirView.Race` is a `ResolvedCode`, `EffectiveInsurancePolicy.Level` is an
`InsuranceLevel`) — never the reverse. `Domain` itself still references nothing of this service's own,
exactly as `LayerDependencyTests.Domain_references_none_of_this_services_own_assemblies` already
enforced; a new test, `ReadModels_references_only_domain`, enforces the new project's own boundary the
same way. `Application.Ports`, `Application`, `Infrastructure`, and `Api` all take a direct reference to
the new project, since each already consumes read-model types directly.

## Consequences

- The write side (`Domain`) is now provably a true leaf with respect to the read side too: nothing an
  aggregate does can ever accidentally depend on a read model, because the compiler would reject it, not
  just a reviewer.
- `EffectiveInsuranceRule` physically left `Domain` even though it's pure business logic with no I/O,
  because it operates exclusively on read-model types — a reasonable home for "rules about the read
  side" that doesn't force `Domain` to reference `Domain.ReadModels` just to keep one rule file in a
  familiar folder.
- `OpenEMR-Next.slnx` and five `.csproj` files gained a new project reference each; no namespace or
  `using` statement anywhere else needed to change, since every type kept its existing namespace.
- This closes the DDD read-model-boundary item from the architecture self-assessment. The
  single-entity-aggregate half of that same dimension was already resolved by `InsuranceCoverage`
  (ADR 0053).

## Alternatives considered

- **A marker interface (`IReadModel`) plus an architecture test, same project.** Smaller change, but the
  boundary would still only be a compile-time marker inside one assembly — a read model could still
  accidentally reference an aggregate with nothing stopping it except the marker-check test. Rejected as
  a half-measure once a real separate-project split was feasible with contained blast radius (five
  `.csproj` edits, zero `using`-statement changes).
- **Leave it as documented debt.** Considered, but the fix turned out cheap enough (no renames needed)
  that deferring it further wasn't worth it.

## Related decisions

Closes one of the four items tracked in `docs/m4-domain-notes.md`'s architecture self-assessment
section, alongside ADR 0053 (the other DDD item), the `AdvanceDirectiveDocument.MaxContentBytes`
invariant (security), and the `ResolvedCode`/`PatientFhirView` renames (clean architecture).
