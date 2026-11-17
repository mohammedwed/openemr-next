# 0068. Clinical Care updates are conditional on the row's last-update stamp and touch only the columns they own

- **Status:** Proposed
- **Date:** 2026-11-17
- **Builds on:** [0064](0064-clinical-care-shares-the-legacy-database-and-coexists-with-legacy-encounter-writers.md), [0065](0065-vitals-are-stored-in-us-units-and-the-api-takes-explicit-units.md)
- **Research:** `docs/clinical-care/05-update-and-audit-research.md`

## Context

Slices 1 to 3 only create. Updating is where the shared database bites: twenty-one legacy files write `form_encounter` and the vitals
form edits `form_vitals` in place, so a read-modify-write from this service can silently undo another writer's change (LEG-8, LEG-10).
Legacy itself has no protection: its update writes every posted column, last writer wins, and we cannot change that.

Both tables already carry a stamp the **database** maintains for every writer: `form_encounter.last_update` (`timestamp`) and
`form_vitals.last_updated` (`datetime`), both `ON UPDATE CURRENT_TIMESTAMP`, whole-second precision. A new version column would be
ignored by legacy and so would detect nothing.

## Decision

1. **The token is the existing stamp.** A read returns it as an `ETag`; an update must send it as `If-Match`. The value is opaque to the
   client.
2. **The write is conditional:** `UPDATE ... SET <owned columns> WHERE id = ? AND last_update = ?`. Zero rows affected is then
   disambiguated by one read: the row is gone (**404**) or it changed (**412 Precondition Failed**, problem details, with the current
   `ETag` in the response so a client can reload).
3. **A missing `If-Match` is 428 Precondition Required.** An unconditional update is never allowed.
4. **Only owned columns are written** (encounter: `date`, `onset_date`, `reason`, `class_code`, `pc_catid`, `facility_id` and
   `facility`, `pos_code`, `provider_id`). Billing, identity and the columns no slice owns are not in the statement, so a billing write
   in the same second is not overwritten and our write does not disturb it. Grants are column-level `UPDATE` on exactly these.
5. **`PUT` replaces the editable set;** there is no `PATCH` (null versus absent is ambiguous on a shared row).
6. **`sensitivity` is not editable** until a permission port exists (F-CC-2).
7. **Each update appends an event** (`EncounterUpdated`, `VitalsCorrected`) to the outbox in the same transaction. Payloads name the
   changed fields, not values (EVT-5).

## Consequences

- **Protects our writes, not legacy's.** A legacy save after ours still overwrites ours without warning. That asymmetry is inherent to
  coexistence and is stated in the slice doc, not hidden.
- **Residual race:** two writes in the same second as the client's read are undetected (whole-second stamp). Accepted; documented.
- **Billing activity causes conflicts** (it bumps the stamp). The right direction; the UI explains it and offers a reload.
- **Pinned by tests:** a conditional-update test against the real database where a second connection (standing in for legacy) changes the
  row between read and write, and a test that the statement names no column outside the owned set.
