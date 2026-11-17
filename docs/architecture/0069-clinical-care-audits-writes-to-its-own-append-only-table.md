# 0069. Clinical Care audits its writes in its own append-only table

- **Status:** Proposed
- **Date:** 2026-11-17
- **Builds on:** [0042](0042-audit-log-scoped-to-login-and-the-bff-proxy.md), [0064](0064-clinical-care-shares-the-legacy-database-and-coexists-with-legacy-encounter-writers.md)
- **Research:** `docs/clinical-care/05-update-and-audit-research.md`

## Context

SEC-9 requires audit that is append-only, written in the business transaction, and separate from logs and events. Clinical Care has none:
creates are recorded only by their outbox event, which is curated and purged. AuthFacade's audit (ADR 0042) covers login and the proxy,
not clinical writes. Legacy's `log` table is written for deletes only, is not append-only, and cannot join our transaction without a
grant on a table with other writers.

## Decision

1. **A dedicated table, `clinical_care_audit`,** in the shared database, created by the seed script. Columns: `id`, `at` (UTC,
   `DATETIME(6)`), `actor` (the caller's subject, 100), `actor_id` (users.id, nullable), `actor_verified` (0 or 1), `action` (a business
   verb, 50: `encounter.create`, `encounter.update`, `vitals.record`, `vitals.correct`, `encounter.delete`, `vitals.delete`), `subject`
   (the record's uuid as `CHAR(36)`), `outcome` (`success`, `denied` or `failed`), `correlation_id` (100), `source_address` (45),
   `details` (a JSON array of changed field **names**, never values, 1000). Text that comes from request headers is cut to its column,
   not rejected: a long header must not fail a clinical write.
2. **Append-only by grant:** the service user gets `INSERT` and `SELECT` on it, never `UPDATE` or `DELETE`.
3. **Same transaction.** The audit insert is part of the registry's unit of work; a change cannot commit without its row. It sits behind an
   `IAuditLog` port (DOM-7) so a different store can replace it.
4. **Every write is audited, and so is every refusal that reached the business layer.**
   - A success row is written by the registry inside the change's transaction (item 3).
   - A refusal (`failed`: the patient or encounter does not exist, or validation failed) is written by the use case as its own statement, since nothing
     changed. It names **the record the caller asked about**: the patient for a refused create (no encounter exists), the encounter for refused vitals.
     That uuid is caller-chosen, may not exist and may be the empty uuid, so only a *success* row must name a real record.
   - **Fail closed:** if the refusal's audit insert fails, the request fails with a 500 instead of answering as though it had been audited.
   - Not audited: requests refused before the business layer (a bad route, a missing secret, a unit the binder rejects) and unexpected errors, which the
     global handler logs. `denied` is written only once a permission port exists.
   - **Known cost:** an invalid-request flood writes a row per request. Rate limiting belongs to the gateway; the table is indexed on `(subject, at)` and
     `at`, and a retention period is a later policy decision (item 7).
5. **The actor is verified or the row says it is not.** Until Phase 1b (identity forwarding) the actor is the caller-asserted provider and
   the row records `actor_verified = 0`. After it, the subject comes from the gateway's validated session.
6. **Reads are not audited in this slice.** Sensitive-read audit needs the permission port and a retention decision; it is recorded as
   open work, not silently skipped.
7. **Retention** follows the retention job's pattern but is **not** purged by it: audit outlives the records it describes. A retention
   period is a policy decision recorded when the table is first used in production.

## Consequences

- One more table and one more grant, both testable in the existing container test.
- Legacy's audit screens will not show Clinical Care's changes (question Q1 in the research doc, answered no). The gap is documented, not
  hidden.
- Audit rows hold identifiers and field names only, so a log leak is not a clinical-data leak (SEC-9).
- Costs one extra insert per write inside the transaction; negligible next to the existing `forms`, `uuid_registry` and outbox writes.
