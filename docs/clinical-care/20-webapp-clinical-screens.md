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
| L1 | **Not browser-verified, no automated UI tests.** Layout, the dialogs' behaviour and accessibility are checked by reading and compiling only. |
| L2 | The acting provider is the placeholder `1`, as in every other dialog, until identity is forwarded (decision 1, finding F-CC-1). |
| L3 | A note's date and an immunization's dates go as wall-clock at offset zero: exact only when the API runs in UTC (decision 9). |
| L4 | No delete anywhere (the permission port). SOAP and clinical notes have no "void" either, unlike immunizations. |
| L5 | The immunization dialog does not offer the linked encounter, the ordering provider or the user id; it preserves them but cannot set them. |
| L6 | Clinical notes show their category by its id, not its label. |
