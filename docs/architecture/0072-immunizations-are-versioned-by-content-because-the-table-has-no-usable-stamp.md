# 0072. Immunizations are versioned by content, because their table has no usable stamp

- **Status:** Accepted
- **Date:** 2026-11-17
- **Builds on:** [0068](0068-clinical-care-updates-are-conditional-on-last-update-and-touch-only-owned-columns.md), [0069](0069-clinical-care-audits-writes-to-its-own-append-only-table.md), [0064](0064-clinical-care-shares-the-legacy-database-and-coexists-with-legacy-encounter-writers.md)
- **Research:** `docs/clinical-care/14-immunizations-research.md`

## Context

ADR 0068 makes every update conditional on a stamp the **database** maintains for every writer (`last_update`, `last_updated`, `modifydate`). `immunizations` has none: `update_date` is `timestamp NOT NULL` with no
default and no `ON UPDATE`, nothing sets it, and legacy's own screen saves with `REPLACE INTO`, which rewrites the whole row without touching it. Legacy's REST and FHIR surface is read-only and its service's write methods are
stubs, so the screen is the writer that matters, and it overwrites whatever is there.

## Decision

1. **The version is a hash of the row's owned columns,** sent as a strong `ETag` of a different shape from a stamp's (`"c-..."`, so the two can never be mistaken for each other). Any writer changing any owned column changes it.
   A change to a column this service does not own does not, which is right: it cannot affect what a correction writes.
2. **The write verifies the version under a row lock.** In one transaction: `SELECT ... FOR UPDATE` the row, rebuild its view with the same mapping the reader uses, hash it, and compare with the version the caller read. Equal: write, with the
   event and the audit row. Different: roll back and report a conflict carrying the current version. The use case also checks the version on a plain read first, for a clean 412 without taking a lock.
   There is no per-column `WHERE` comparison: it would have to compare a 4-byte `float` and several nullable columns exactly, and one lock-and-verify is simpler and checkable.
3. **The hash covers a canonical text of the view,** with invariant formats, **length-prefixed parts** and a distinct marker for an absent part, and rounded values (an amount to four places, dates as ticks), so the same row always gives
   the same version, and two different rows never share a canonical text by accident of concatenation (`"ab","c"` versus `"a","bc"`, or a missing value versus an empty string).
4. **We maintain `update_date` ourselves** (set to now on a correction, and on a record) so legacy's FHIR reader, which exposes it as the resource's last-updated time, sees a real value. It is **not** the token.
5. **`added_erroneously` ("entered in error") is an editable field,** the non-destructive way to void a mistaken entry. There is no delete here: legacy's delete is a hard delete needing `admin/super`, and waits for the permission port.
6. **Everything else follows the established pattern:** one transaction for the row, its `uuid_registry` row, the outbox row and the audit row (a failing audit rolls it back); refusals audited; an unknown actor on corrections; the order of checks is
   a malformed body (400), then 428, then 404, then 412, then domain rules (400); a no-op writes nothing; column-level grants; the uuid-less rows legacy creates are not addressable.

## Consequences

- **This is a different mechanism from every other update in the service,** and it is documented as such. The cost is a lock per correction; the table is small per patient and corrections are rare.
- **The version is only as complete as the owned set:** if a later slice adds an owned column, the canonical text must include it (a unit test lists every property of the view and fails if one is neither in the hash nor deliberately outside it, so it cannot be forgotten).
- **Legacy's `REPLACE` still wins silently** if it runs after us, and it also resets columns we do not own. That asymmetry is inherent to coexistence and is stated, not hidden.
- If legacy ever adds a maintained stamp to this table, this ADR can be superseded and the token swapped without changing the API: the `ETag` is opaque.
