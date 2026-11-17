# 0074. Clinical notes get their own parent form, its id allocated under a database lock

- **Status:** Accepted
- **Date:** 2026-11-17
- **Builds on:** [0068](0068-clinical-care-updates-are-conditional-on-last-update-and-touch-only-owned-columns.md), [0069](0069-clinical-care-audits-writes-to-its-own-append-only-table.md), [0064](0064-clinical-care-shares-the-legacy-database-and-coexists-with-legacy-encounter-writers.md)
- **Research and rules:** `docs/clinical-care/19-clinical-notes-slice.md`

## Context

`form_clinical_notes` differs from the other forms. It has a uuid and a database-maintained stamp (`last_updated ON UPDATE`), so identity and concurrency follow the vitals pattern. But **one `forms` row (a "parent form") groups several note rows** that share a `form_id`, and legacy gives a
new parent `form_id = MAX(form_id) + 1` over the table, with no lock and no unique key. Two concurrent writers can take the same id and silently merge two encounters' notes under one parent. A note's "delete" is `activity = 0`, and the legacy form copies `code` and `codetext` from the chosen type with script.

## Decision

1. **Each note gets its own parent form** (one `forms` row, one note row), so a note is never attached to a group someone else is editing. A legacy parent that groups several notes stays readable: a note matches its encounter through its parent's `forms` row by `form_id`, with `EXISTS`.
2. **The new `form_id` is allocated under a named database lock** (`GET_LOCK`), taken on an explicitly held connection **before the transaction begins**, and released after the commit or rollback. It must be before: under REPEATABLE READ the transaction's snapshot is fixed by its first read, so a writer that waited for the
   lock inside its transaction would still read the table as it was before the winner committed and take the same id. (A test with eight simultaneous writers found exactly this.) The lock serialises this service's writers; it cannot serialise legacy's, which takes none (L3).
3. **A note belongs to its encounter by two agreeing facts:** its own `pid` and `encounter` columns and its parent's `forms` row. Reads and updates require both, and only `activity = 1` notes: legacy retires a note by setting it to 0, so an inactive note is not found.
4. **`code` and `codetext` are derived from the type** (the LOINC code in the option's `notes`, and its title), not accepted from the caller, so a note cannot carry a code that disagrees with its type. A correction that changes the type rewrites both.
5. **Concurrency uses the existing stamp** (`last_updated`), as vitals do (ADR 0068): `If-Match` is a stamp `ETag`.
6. **Retiring a note (`activity = 0`) is not offered:** it is a delete, which waits for the permission port. The correction does not grant or write `activity`.

## Consequences

- A lock around a write is new in this service and is limited to this one allocation. Its cost is a short serial section per new note.
- Legacy's `MAX + 1` race with this service's writers is not closed, only narrowed to the legacy writer's own side. Stated in the slice doc.
- The lookups need two more columns of `list_options` (`notes`, `activity`), granted and tested.
