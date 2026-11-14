# 0053. InsuranceCoverage is a multi-entity aggregate cluster, owning the swap decision

- **Status:** Accepted
- **Date:** 2026-11-14
- **Builds on:** [0049](0049-patient-management-writes-into-the-legacy-database.md), `docs/m4-full-legacy-parity-plan.md` (insurance data, feature 2)

## Context

A self-critical review of the Domain layer (recorded in `docs/m4-domain-notes.md`) scored DDD design at
8/10 but flagged a real, not cosmetic, gap: every aggregate in this project (`Patient`,
`InsurancePolicy`, `AdvanceDirectiveDocument`) is single-entity. None of them had ever needed to enforce
an invariant *across* a cluster of child entities — the harder half of what "aggregate" means in DDD was
never actually exercised.

Legacy's own `InsuranceService::swapInsurance()` is a genuine candidate, not an invented one: it moves
one policy to a target level (primary/secondary/tertiary) and, if another policy already occupies that
level, trades the two policies' levels. That is inherently a two-entity operation — deciding "who ends up
at which level" cannot be scoped to either policy alone.

The existing `IInsurancePolicyRepository.SwapAsync` did this as a single raw-SQL method: read both rows,
decide inline which gets which type, issue the UPDATEs, with a sentinel-date (`9999-12-31`) workaround for
the `(pid, type, date)` unique constraint along the way. The decision logic lived in Infrastructure, not
Domain — untested except via a real database, and not expressible as a Domain rule at all.

## Decision

1. **`InsuranceCoverage` is a new aggregate root**: one patient's whole cluster of `InsurancePolicy`
   entities. `InsuranceCoverage.For(pid, existingPolicies)` reconstitutes the cluster (there is no
   "create" for a patient's coverage as a whole — only ever "here's what currently exists," the same
   shape a shopping-cart aggregate takes). It owns:
   - `AddPolicy(...)`, which adds the one invariant that only makes sense across the whole cluster: no
     two policies at the same level with the same start date. (`InsurancePolicy.Add`'s own
     field-level validation still runs underneath it — this does not replace that, it adds to it.)
   - `Swap(policyUuid, targetLevel)`, which decides the business question legacy's `swapInsurance()`
     answers — which policy ends up at which level — and returns an `InsuranceSwapResult(Source,
     DisplacedOccupant)` record describing the outcome.
2. **`InsurancePolicy.Level` changes from a constructor-only field to `{ get; private set; }`**, with a
   new `internal void ChangeLevel(InsuranceLevel)` — the one mutation `InsuranceCoverage.Swap` needs.
   Scoped `internal` (via the same `InternalsVisibleTo` pattern `AssignGeneratedIdentity` already uses)
   so nothing outside the Insurance cluster, including Application-layer code, can change a policy's
   level directly.
3. **`InsurancePolicy` gains a second, `internal` reconstitution constructor** (`Reconstitute`, called
   only by `EfInsurancePolicyRepository`) that restores a policy's real `Id`/`Uuid` without re-running
   `Add`'s validation — a row already in the database passed that validation once already.
4. **The sentinel-date workaround stays in Infrastructure, not the aggregate.** `InsuranceCoverage.Swap`
   decides levels only; it has no awareness of the `(pid, type, date)` unique constraint, because that
   constraint is a persistence detail, not something this patient's coverage cares about as a business
   rule. `EfInsurancePolicyRepository.PersistSwapAsync` detects the date collision (comparing the two
   policies' `StartDate`s, which the aggregate leaves untouched) and applies the same workaround the old
   `SwapAsync` used, now driven by the aggregate's already-decided `InsuranceSwapResult`.
5. **The port shape changes**: `IInsurancePolicyRepository.SwapAsync(pid, targetLevel, policyUuid, ct)` is
   replaced by `GetPoliciesAsync(pid, ct)` (loads the cluster) and `PersistSwapAsync(InsuranceSwapResult,
   ct)` (persists its decision). `SwapInsuranceUseCase.ExecuteAsync`'s own public signature is unchanged —
   it now orchestrates `GetPoliciesAsync` → `InsuranceCoverage.For` → `.Swap` → `PersistSwapAsync`
   internally, so `InsuranceEndpoints` and its tests needed no changes.

## Consequences

- The swap decision — which policy ends up at which level, including picking the most-recently-started
  policy when more than one somehow occupies the target level — is now independently unit-testable
  (`InsuranceCoverageTests`) without a database. The old `SwapAsync` could only be exercised through a
  real Testcontainers MariaDB.
- `InsurancePolicy` is no longer fully immutable after construction (`Level` can change via `ChangeLevel`)
  — a deliberate, narrow exception to the validate-on-construction pattern every other aggregate/value
  object in this layer follows, justified by this being the one legitimate post-construction business
  mutation the cluster needs, the same way `AssignGeneratedIdentity` is the one exception for database-
  generated identity.
- `GetPoliciesAsync` requires every row for a patient to have a real, parseable `DateOnly` — unlike
  `GetRowsAsync` (used by `EffectiveInsuranceRule`), which deliberately tolerates legacy's zero/partial
  dates. This is fine for the swap operation specifically: both the policy being moved and whatever
  occupies the target level are rows this service itself created through `InsurancePolicy.Add`, which
  never lets a bad date through. A pre-existing legacy row with a zero date cannot participate in a swap
  through this path — acceptable, since such a row could never have been a valid swap target under this
  service's own validation anyway.
- Remaining architecture gaps from the same self-review are tracked, not resolved by this ADR: Domain's
  read models (`PatientFhirView`, `InsurancePolicyRow`) still sit in the same project as the aggregates
  with no sharper boundary than a folder, and `PatientFhirView`/`ResolvedCode` still encode FHIR wire
  vocabulary directly rather than translating fully at the Api/Infrastructure boundary. See
  `docs/m4-domain-notes.md`'s architecture self-assessment section.

## Alternatives considered

- **Keep the swap decision in Infrastructure, just extract it into a private helper method.** Rejected:
  it would still be untestable without a database and still wouldn't be a Domain concept — the whole
  point was to exercise a genuine multi-entity invariant inside Domain, not to tidy up SQL.
- **Make `InsuranceCoverage` own persistence too (an `AddPolicyAsync`/`SwapAsync` on the aggregate
  itself).** Rejected: breaks the hexagonal boundary every other aggregate in this layer respects —
  Domain has zero I/O awareness by design (`LayerDependencyTests`), and this aggregate shouldn't be the
  first exception.

## Related decisions

Builds on [0049](0049-patient-management-writes-into-the-legacy-database.md) (why this writes directly
into legacy's `insurance_data` table) and the insurance-data research in
`docs/m4-full-legacy-parity-plan.md`.
