# Clinical Care M5d: WebApp screens for the new clinical APIs

- **Milestone:** ROADMAP M5d. **Builds on** slices 1 to 4 ([13](13-correct-vitals-slice.md), [15](15-record-immunization-slice.md), [17](17-soap-notes-slice.md), [19](19-clinical-notes-slice.md), [18](18-lookups-slice.md)) and the WebApp conventions of ADR 0067 and 0071.
- **Status:** built, **compile-checked only**. The WebApp has no automated tests and no screen here has been opened in a browser (that needs a signed-in user).

## 1. What was added

| Screen | Where | What it does |
|---|---|---|
| **Correct vitals** | A "Correct" button on each row of the encounter page's vitals card; `RecordVitalsDialog` gains an edit mode | Re-reads the record when it opens, **in US units** (as stored, so nothing is converted and converted back), prefills it, and saves against that fresh `ETag`. A measurement you clear is removed; at least one must remain. |
| **SOAP notes** | A card on the encounter page, `EncounterSoapNotesPanel` + `SoapNoteDialog` | Lists the encounter's notes (paged), records and edits the four sections. |
| **Clinical notes** | A card on the encounter page, `EncounterClinicalNotesPanel` + `ClinicalNoteDialog` | Lists, records and edits notes. Type and category are selects from the lookups; a type or category legacy has since retired stays selectable on a note that already has it. |
| **Immunizations** | An "Immunizations" tab on the patient page, `PatientImmunizationsPanel` + `ImmunizationDialog` | Lists (paged, with vaccine names), records and edits. Coded fields are selects from one options call; "Entered in error" voids a mistaken entry (there is no delete). |
| **Encounter lookups** | `CreateEncounterDialog` (done earlier in M5d) | Class, visit category and facility selects, with a fallback to the old fields. |

All follow the existing panel and dialog conventions: a server-paged card that loads itself, a failed first load with "Try again", a failed page change keeping the last page with a warning, an edit based on the version the row was listed with, and a **stale** (412) refusal that closes the dialog with "Close and reload" and a warning snackbar. Dates go as wall-clock at offset zero (`IssueWire`, pending decision 9).

## 2. Findings made while building them

1. **A correction replaces the whole set, so leaving a field out clears it.** The immunization dialog does not show the linked encounter, the ordering provider or the user id of who gave it, so it would have wiped them. The API now returns the linked encounter's `encounterUuid` (found from its number and the patient, not part of the version), and the dialog sends all three back as listed. A test pins both behaviours: sending the link back keeps it, leaving it out clears it.
2. **The immunization dialog needed titled options for seven lists,** which no API offered. `GET /api/v1/lookups/immunization-options` returns them in one call (active, legacy's order, whole-number vaccines only).
3. **Editing keeps the stored seconds.** The time picker has minute resolution, so a save that left the time alone would have silently dropped the seconds of the administered time; it is kept when the picker still shows the same minute.

## 3. Known limitations

| # | Limitation |
|---|---|
| L1 | **Not browser-verified; automated UI tests cover delete only.** Layout and accessibility are checked by reading and compiling; the component tests (section 8) cover the three delete flows and nothing else yet. |
| L2 | ~~The acting provider is the placeholder `1`~~ **Resolved (ADR 0075):** the dialogs send no provider; the signed-in user is the actor. |
| L3 | A note's date and an immunization's dates go as wall-clock at offset zero: exact only when the API runs in UTC (decision 9). |
| L4 | **Delete exists for vitals, SOAP notes and clinical notes** (section 7, ADR 0076), not for problems, allergies or immunizations (which have "entered in error"). |
| L5 | The immunization dialog does not offer the linked encounter, the ordering provider or the user id; it preserves them but cannot set them. |
| L6 | Clinical notes show their category by its id, not its label. |

## 7. Delete (M5g)

Each vitals row, SOAP note row and clinical note row has a **Delete** button (red text, named per row for screen readers). It asks first ("Delete these vitals?" and the like, saying the record is marked deleted, not removed, and that an administrator can restore it in OpenEMR), then calls the `DELETE` endpoint with the row's `ETag`. One helper, `Shared/RecordDeletion.cs`, words the outcomes: a **403** is "Only an administrator can delete records"; a **404** or **412** says the record was already gone or changed and reloads the list; a network failure says nothing was deleted. Deleting the last row of a later page steps back one page. The button is offered to everyone because the session does not carry rights (ADR 0076). Compile-checked only: not yet seen in a browser.

## 8. Component tests (M5i)

`tests/Services/WebApp/OpenEmrNext.WebApp.UnitTests` is the first automated test project for the WebApp. It uses bUnit (2.15) to render the **real** components, with MudBlazor's real services, dialog provider and snackbar provider, and replaces only the typed clinical care client with a substitute (NSubstitute) that the test scripts. No browser and no Docker; the whole project runs in about a second, and CI picks it up through the solution file.

`RecordDeletionPanelTests` runs the same six scenarios against the vitals, SOAP and clinical note panels (24 tests): every row offers a Delete named for the record; nothing is sent until the user confirms, and "Keep it" sends nothing and does not reload; confirming sends the row's own `ETag` for the right encounter and record, shows "Deleted." and reloads the list; a 403 says only an administrator can delete and leaves the list alone; a 404 or 412 says the record is gone or changed, deleted nothing, and reloads; a network failure says nothing was deleted; the confirmation says the record is marked, not removed.

**Mutation check:** with the 403 handling made to claim success, 3 tests fail (one per panel); with a wrong `ETag` sent from the SOAP panel, 4 fail. Both restored.

**What this does not cover:** layout, styling, keyboard and screen-reader behaviour, the other dialogs and panels, and anything that needs a real browser or the live stack. The browser check is still outstanding.
