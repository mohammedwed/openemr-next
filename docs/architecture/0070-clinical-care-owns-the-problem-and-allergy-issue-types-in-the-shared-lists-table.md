# 0070. Clinical Care owns the problem and allergy issue types in the shared `lists` table

- **Status:** Accepted (built with the problem slice, `docs/clinical-care/08-record-problem-slice.md`)
- **Date:** 2026-11-17
- **Builds on:** [0064](0064-clinical-care-shares-the-legacy-database-and-coexists-with-legacy-encounter-writers.md), [0068](0068-clinical-care-updates-are-conditional-on-last-update-and-touch-only-owned-columns.md), [0069](0069-clinical-care-audits-writes-to-its-own-append-only-table.md)
- **Research:** `docs/clinical-care/07-problems-and-allergies-research.md`

## Context

Legacy stores problems, allergies, medications, surgeries, dental issues and devices in **one table, `lists`**, told apart by a free-text `type` that a
site can extend (`issue_types`). The DDD blueprint is inconsistent about who owns this: "Patient Issue" appears in Patient Management's glossary, while
Condition and AllergyIntolerance are Clinical Care aggregates and medications belong to Pharmacy. Ten legacy files write the table by three routes
(the issue screen, REST/FHIR, imports), and the routes disagree about uuids, timestamps and deletion.

## Decision

1. **Clinical Care owns exactly two issue types:** `medical_problem` (aggregate `Problem`) and `allergy` (aggregate `Allergy`). "Patient Issue" is not a
   context and not an aggregate here: it is a legacy storage convention. Every other type stays with legacy until a context owns it.
2. **Ownership is enforced by `type` in every statement.** Each read, insert and update carries `type = '<this aggregate's type>'`. A column-level
   grant cannot restrict rows, so this is a code rule pinned by tests that try to reach a row of another type by id and by uuid.
3. **Two aggregates, one adapter helper.** `Problem` and `Allergy` have separate rules and endpoints; one internal helper knows the `lists` layout, so
   the legacy mapping lives in one place (LEG-2).
4. **The concurrency token is `lists.modifydate`** (ADR 0068, unchanged model): an `ETag` on reads, `If-Match` on writes, a conditional `UPDATE` on owned
   columns only, 428 and 412. A resolved problem is an update (`enddate`, `outcome`), so this slice has no delete.
5. **Create follows the REST path where the two legacy paths disagree:** the uuid is assigned and registered at insert, and `date` is `NOW()` (the issue
   screen's `date("Y-m-d H:m:s")` writes the month into the minutes). The issue screen's `lists_touch` row is also written: legacy's patient summary reads
   it (`getListTouch`) as the `listTouched` flag of the allergy, problem and medication cards, meaning "this list has been addressed" (an explicit
   "nothing" included).
6. **A row with no uuid is not addressable** and is left out of lists (the encounter precedent, CC-16). This service does not backfill uuids on read.
7. **Audit and events as in ADR 0069:** every write has an audit row in its own transaction; events name fields, never values.
8. **Delete is not built here.** It waits for the permission port (legacy needs `admin/super` for the UI delete). Legacy's hard delete is never offered.

## Consequences

- One shared table with several future owners means the `type` filter is a correctness rule, not an optimisation; a missing filter would let one
  context edit another's data. The tests make that failure loud.
- The `H:m:s` date bug is **not** reproduced. Rows created here sort correctly; rows created by the issue screen keep the wrong minutes.
- The `uuid`-less window is accepted: a problem created in the issue screen is invisible here until legacy assigns it a uuid.
- **The grants are column-level** because the table is shared: `SELECT` and `INSERT` on only the columns a problem uses (slice doc, section 3). A
  later allergy slice adds its three columns (`reaction`, `severity_al`, `verification`) the same way.
- **Updates are conditional on `modifydate` and type-scoped** (slice 3, `10-update-problem-and-allergy-slice.md`): the `UPDATE` grant names the eleven owned columns, and a
  problem update can never reach an allergy or a medication by its uuid.
- **The `type` rule is now structural:** an architecture test reads the SQL and fails the build if any statement on `lists` omits `type` (mutation-tested; `12-shadow-comparison-and-review.md`).
- **Links to encounters follow the same ownership** (slice 4, `11-link-issue-to-encounter-slice.md`): only problems and allergies can be linked, a medication's uuid is a 404, and a link
  between two patients' records, which legacy allows, is refused.
- Medications, surgeries, dental issues and devices remain unowned; Pharmacy will have to adopt `medication` and respect the same `type` rule.
