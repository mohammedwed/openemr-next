# 0080. Deleting a problem or an allergy removes the row, as legacy does

- **Status:** Accepted
- **Date:** 2026-11-17
- **Builds on:** [0068](0068-clinical-care-updates-are-conditional-on-the-version-the-caller-read.md), [0070](0070-problems-and-allergies-are-two-owned-types-in-the-shared-lists-table.md), [0076](0076-deleting-records-needs-legacys-admin-super-right-and-is-a-flag-not-a-removal.md), [0077](0077-creating-requests-accept-an-idempotency-key-and-soap-notes-guard-their-own-retries.md)
- **Closes:** the "Not decided" item of ADR 0076 (problems and allergies).

## Context

ADR 0076 gave vitals, SOAP notes and clinical notes a delete that sets legacy's own flag, and left problems and allergies undecided, because the flag does not work for them. Read from legacy's source:

- Legacy **removes** an issue: `interface/patient_file/deleter.php` deletes the `issue_encounter` links, any `lists_medication` row and the `lists` row itself, after `AclMain::aclCheckCore('admin', 'super')`. Its REST services run `DELETE FROM lists`.
- Its issue screens (`stats_full.php`) list **every** row of the type with no `activity` filter. A row flagged `activity = 0` would therefore disappear from this application and **still be listed in OpenEMR**.

The user was asked on Nov 17 and chose to **hard delete, like legacy**, over a soft delete here (which would leave the two systems disagreeing about what exists) and over leaving issues without a delete.

## Decision

1. **`DELETE /api/v1/problems/{uuid}` and `DELETE /api/v1/allergies/{uuid}` remove the row** and its links to encounters (`issue_encounter`), in one transaction, as legacy's `deleter.php` does. `lists_medication` is not touched: it belongs to medications, which this service does not own.
2. **Same rules as every other deletion (ADR 0076):** a verified user who holds legacy's `admin/super` right; `If-Match` with the `modifydate` stamp, verified under a row lock (428, 412, and two simultaneous deletions succeed once); the answers ordered 401, 428, 403, 404, 412 so a refusal reveals nothing about which records exist; every refusal audited as failed.
3. **One transaction** holds the removal, the outbox event (`ProblemDeleted`, `AllergyDeleted`: the issue's uuid and the patient's uuid and number, no clinical text, so a consumer holding a copy knows to drop it) and the audit row (`problem.delete`, `allergy.delete`).
4. **A row is reached only by its uuid and its type**, as every statement on `lists` is. A medication, a surgery or any other type is a 404 whichever route it is asked through, and is never deleted.
5. **The database grant widens, and this is the cost of the decision:** `DELETE` on `lists`. MariaDB cannot grant `DELETE` by column or by value, so the service's user can now, in principle, remove any row of that table. The restriction is in the code (the architecture test that every statement names its type, and the adapter's own filter) and is pinned by tests that try to delete rows of other types by uuid. It is stated here because it is the widest grant the service holds on a table it shares with other record types.
6. **What is lost:** the record cannot be restored. Legacy's own deletion writes the whole row into its log; this service's audit row deliberately does not (it holds no clinical text), so **the content of a deleted issue is not recoverable from this service**. Resolving a problem or ending an allergy remains the way to retire one while keeping it on the record, and the WebApp says so in its confirmation.

## Consequences

- The two UIs agree about what exists; a problem deleted here is gone in OpenEMR and the other way round.
- An encounter that listed the issue no longer does (its link is gone with it), which is also what legacy does and what its REST path fails to do.
- The WebApp offers Delete on each problem and allergy row, with a confirmation that says it cannot be undone, and explains a 403 as "only an administrator can delete".
