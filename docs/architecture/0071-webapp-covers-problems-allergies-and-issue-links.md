# 0071. WebApp's frontend extends to problems, allergies and their links to encounters

- **Status:** Accepted
- **Date:** 2026-11-17
- **Builds on:** [0067](0067-webapp-covers-encounters-and-vitals.md), [0068](0068-clinical-care-updates-are-conditional-on-last-update-and-touch-only-owned-columns.md), [0070](0070-clinical-care-owns-the-problem-and-allergy-issue-types-in-the-shared-lists-table.md)

## Context

The Clinical Care API now records, updates, lists and links problems and allergies (M5c slices 1 to 4), and nothing in the WebApp called it. Three facts of that API shape the screens more than
the legacy issue screen does:

- **An update needs the version the caller read** (`If-Match`, ADR 0068), and the generated client could not send it: the three `PUT`s read the header off the raw request, so the OpenAPI document did
  not declare it. A list also has no per-item header to read a version from.
- **There are no lookup endpoints for the code lists** (outcome, occurrence, reaction, severity, verification), as for the encounter form (pending decisions, item 10).
- **Dates cross a time zone** (pending decisions, item 9), and a problem or an allergy has more date-only fields than an encounter, re-sent by one-click actions.

## Decision

1. **The API contract was fixed first, in its own commit.** `If-Match` is a declared header parameter on every `PUT`, and `EncounterResponse`, `ProblemResponse` and `AllergyResponse` carry an `eTag` equal
   to the `ETag` header, so a listed row can be updated without a read per row. This is a change to Clinical Care, not the WebApp, and is why the screens could be built at all.
2. **Two new tabs on the patient page, Problems and Allergies,** in the style of the Encounters tab: a server-paged list that owns its page, takes a ticket per load, reports its total for the tab label,
   and shows a warning over the last good data when a page change fails. A tab's count appears after its first visit, because a tab's content is created when it is first shown.
3. **Recording and editing share one dialog per kind** (`ProblemDialog`, `AllergyDialog`): the fields are the same and an update replaces the whole set. A dialog closes only when the save succeeds, or hands back
   a marker when the record turned out to be **stale (412)**, in which case the only offered action is "Close and reload": saving again would fail the same way.
4. **Resolve, End and Reopen are one-click updates** (the issue screen shows a problem as resolved by its end date, not a flag, ADR 0070). They re-send every other field exactly as listed, use the version the row
   was listed with, and **always reload afterwards**, so a refusal for staleness shows what is stored rather than leaving an out-of-date row on screen.
5. **The encounter page gains a "Problems and allergies addressed" card** above the vitals: a list, a dialog that offers the patient's own problems and allergies minus those already linked, and an Unlink
   action that **asks first**. It asks although a relation is removed, not a record, because one click cannot undo it, and it states that the issue itself is untouched.
6. **Dates go as the same wall-clock time at offset zero** (`IssueWire`), keeping any time of day the stored row had. This is exact when the API runs in UTC and wrong when it does not; the encounter screens
   are unchanged. It narrows item 9, it does not close it (recorded there).
7. **Code lists are fixed lists in `CodeLabels`**, mirroring the legacy snapshot, as the encounter classes are. A site with different options gets the API's 400 with its message, not a silent wrong save.
8. **The provider id is the same placeholder (`1`)** as the other dialogs, for creates only; an update names no provider (ADR 0069 #5). Item 1 still tracks it.

## Consequences

- **None of this has been exercised in a browser.** It compiles with no warnings, and everything it calls is covered by the API's own tests, but the WebApp still has no automated tests (item 7), and checking it needs a
  re-seeded database, rebuilt images and a signed-in user. That check is outstanding, as it still is for the encounter screens.
- A user who edits a row that someone changes in the legacy issue screen sees a clear message and a reload, never a silent overwrite of the other writer's change. The reverse (the legacy screen overwriting ours) is
  still silent and outside this service's control (ADR 0068).
- Medications, surgeries and other types do not appear: those rows exist in the same table but have no owner (ADR 0070), and the patient page says nothing about them.
- The generated client's `UpdateProblemAsync(uuid, if_Match, body)` shape is now part of the WebApp's contract with Clinical Care; the OpenAPI test that pins the header keeps it from silently disappearing.
