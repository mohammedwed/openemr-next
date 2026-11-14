# 0055. Patient Management's database connection retries transient failures

- **Status:** Accepted
- **Date:** 2026-11-14
- **Builds on:** `docs/m4-domain-notes.md`'s Resilience and failing fast dimension

## Context

A new review dimension, Resilience and failing fast, found that `PatientManagementDbContext`'s MySQL
connection had no transient-fault handling at all: `options.UseMySql(connectionString, ...)` with no
`EnableRetryOnFailure()`. A brief network blip to the shared MariaDB database failed every in-flight
request outright, with no automatic recovery -- unlike AuthFacade, which has a dedicated `Resilience/`
folder (Polly-based circuit breakers) guarding its own external dependency (OpenEMR's HTTP API).
Patient Management's external dependency is the database, not HTTP, so the same circuit-breaker
machinery didn't directly apply, but the underlying problem -- no resilience against a transient
failure in the one dependency every request needs -- was the same shape.

Turning on `EnableRetryOnFailure()` is not a safe one-line change here: this service uses explicit,
user-initiated transactions in three places (`EfInsurancePolicyRepository.AddAsync`/`PersistSwapAsync`,
`EfAdvanceDirectiveRepository.AddDocumentAsync`), and EF Core's retrying execution strategy refuses to
run inside a transaction it didn't open itself -- it throws `InvalidOperationException` the first time
a retry actually fires, not at startup, making this easy to ship broken without noticing until a real
transient failure occurs in production.

## Decision

1. **`EnableRetryOnFailure()` is on** for `PatientManagementDbContext`'s connection
   (`PatientManagementApiExtensions.AddPatientManagementHttpDependencies`).
2. **Every explicit transaction is wrapped in `Database.CreateExecutionStrategy().ExecuteAsync(...)`**,
   not called via bare `BeginTransactionAsync`/`CommitAsync`, so the whole unit of work (not just
   individual commands) is what gets replayed on a transient failure.
3. **Any aggregate mutation that depends on a value generated inside the transaction moves outside the
   retried delegate.** `AssignGeneratedIdentity` throws if called twice; a retried delegate re-executes
   from scratch (including generating a fresh id), so calling it *inside* the delegate would throw on
   the second attempt of any retry. Both `AddAsync` and `AddDocumentAsync` now return the generated id
   from the delegate and call `AssignGeneratedIdentity` exactly once, after the strategy call succeeds.
   `PersistSwapAsync` needed no such change -- it mutates no external object as a side effect of
   running; its UPDATEs re-apply the same already-decided values safely on a retry.
4. **Read-only and non-transactional paths needed no change.** `EfPatientFhirRepository`,
   `EfDuplicateScorer`, `EfInsurancePolicyRepository.GetRowsAsync`/`GetPoliciesAsync`, and
   `EfAdvanceDirectiveRepository`'s read methods issue bare, non-transactional commands; EF's retrying
   execution strategy wraps those automatically. `EfPatientRegistry.RegisterAsync` uses EF's own
   implicit `SaveChangesAsync` transaction, which the execution strategy already handles natively.

## Consequences

- Verified against a real Testcontainers MariaDB, not just by reasoning about the API: all 46
  integration tests pass, including every insurance-add/swap and advance-directive-upload test that
  exercises a transaction. An incorrect execution-strategy wrapping would have thrown
  `InvalidOperationException` immediately on any transaction use, not only during an actual retry, so
  this is a meaningful confirmation, not just "it compiled."
- A future repository that adds its own explicit transaction must follow the same
  `CreateExecutionStrategy().ExecuteAsync(...)` pattern, and must not mutate an external aggregate
  inside the retried delegate using a value generated during that same delegate -- both easy to get
  wrong silently (the failure mode only appears under an actual transient fault, not in normal testing
  against a stable database).
- Global exception-handling middleware (`UseExceptionHandler`/`ProblemDetails`) remains a separate,
  unaddressed finding from the same review -- confirmed as a repo-wide gap (`AuthFacade`/`Gateway` lack
  it too), tracked in `m4-domain-notes.md`, not fixed here.

## Alternatives considered

- **Hand-roll a Polly retry pipeline around each raw SQL call**, mirroring AuthFacade's own
  circuit-breaker pattern. Rejected: EF Core's built-in `EnableRetryOnFailure()` already does this at
  the connection level for every command, with no need to wrap each individual `SqlQueryRaw`/
  `ExecuteSqlInterpolatedAsync` call by hand -- Polly's value in AuthFacade is the *circuit breaker*
  behavior (stop calling a dependency that's confirmed down), which doesn't have an obvious database
  equivalent here and wasn't what this finding was about.
- **Leave it unfixed, same as the global exception handler finding.** Rejected: unlike the exception
  handler (a response-shaping concern), an un-retried transient database failure means a request that
  should have succeeded doesn't, for every single request in flight during a blip -- a correctness/
  availability concern worth fixing now rather than deferring.
