# 0079. Clinical Care serves read-only FHIR for five resources

- **Status:** Proposed. Nothing is built; each decision below has a recommendation and needs the user's acceptance first (the scope rule for M5e).
- **Date:** 2026-11-17
- **Builds on:** [0052](0052-firely-net-fhir-sdk-replaces-hand-rolled-fhir-mapping.md) (Firely SDK, vendored US Core), [0072](0072-immunizations-are-versioned-by-a-content-hash-because-the-table-has-no-stamp.md) (content-hash versions), [0075](0075-the-gateway-validates-the-session-and-forwards-the-verified-user.md) (sign-in at the gateway)
- **Closes:** M5e in the roadmap, once accepted and built.

## Context

Legacy OpenEMR serves read-only FHIR R4 for the records Clinical Care owns (`FhirEncounterService`, `FhirConditionService`, `FhirAllergyIntoleranceService`, `FhirObservationService` for vitals, `FhirImmunizationService`). Patient Management already serves `Patient` with the Firely SDK and US Core validation (ADR 0052). Clinical Care has no FHIR surface, so a FHIR client of the new system can read a patient and nothing clinical.

Read from legacy's source (the 8.x tree) and from this repository's own read models:

- **Encounters, problems, allergies and immunizations have a uuid** of their own (`form_encounter.uuid`, `lists.uuid`, `immunizations.uuid`). Legacy's FHIR id for each is that uuid.
- **Vitals are the odd one.** Legacy's `Observation` service turns one `form_vitals` row into **several** Observations: one per measurement (LOINC-coded) plus a blood-pressure panel, a BMI and so on, and gives each its **own uuid**, recorded in legacy's `uuid_mapping` table (`resource`, `target_uuid` = the vitals row's uuid, `uuid` = the observation's). A vitals set therefore has no single FHIR id.
- **Timestamps:** encounters have `last_update`, vitals and clinical notes `last_updated`, problems and allergies `modifydate`. **Immunizations have no reliable stamp** (ADR 0072: the table's `update_date` is not maintained by every writer), which is why their JSON `ETag` is a content hash.
- Legacy reaches practitioners, organizations and locations through their own FHIR resources; Clinical Care has none of those and cannot resolve a reference to them.

## Decisions proposed

Each is a recommendation; the alternatives are named so the choice is visible.

1. **Five resources, read and search only.** `Encounter`, `Condition` (problem-list items), `AllergyIntolerance`, `Observation` (vital signs) and `Immunization`. Writes stay on the JSON API, as in the roadmap. SOAP notes and clinical notes have no FHIR mapping in legacy's resource list for this slice and are out (a `DocumentReference` mapping is a separate decision).
2. **Identity: the record's own uuid is the FHIR id** for encounters, problems, allergies and immunizations.
3. **Vitals: one `Observation` per recorded measurement plus the blood-pressure panel, and the id is derived deterministically** (a name-based UUID from the vitals uuid and the LOINC code), so the same vitals always give the same ids, with no writes. *Alternative:* read legacy's `uuid_mapping` rows so that ids match legacy's. Legacy creates those rows lazily the first time its own FHIR service reads a vitals set, so a set recorded here and never read by legacy has none, and Clinical Care does not hold (and should not be granted) write access to that table. *Recommendation:* derived ids, and **accept that an id minted by legacy for the same measurement differs from ours**; a client must not mix the two sources. This is the one place parity with legacy ids is deliberately given up.
4. **`meta.lastUpdated`** comes from the record's stamp where there is one (`last_update`, `last_updated`, `modifydate`) and is **absent for immunizations**, never guessed. `_lastUpdated` search is offered only for the four that have a stamp.
5. **`ETag` and `meta.versionId`:** a read of one resource returns a **weak** `ETag` equal to the JSON API's token for the same record, and `meta.versionId` the same token without the quotes. The two surfaces therefore agree on whether a record changed. There is no `If-Match` on FHIR (read-only).
6. **Search parameters, kept small:** `patient` (a patient uuid; required on every search except `_id`), `_id`, `_lastUpdated` (where decision 4 allows), `_count` (default 20, maximum 100) and, for `Observation` and `Condition`, `category`. A search returns a `searchset` `Bundle` with `total` and a `next` link. **An unsupported parameter is a 400 `OperationOutcome`, not silently ignored:** a clinical search that quietly drops a filter returns the wrong set. *Alternative:* ignore with a warning, as the FHIR specification allows.
7. **People and places are referenced, not resolved.** `participant`, `performer` and `serviceProvider` carry a literal reference (`Practitioner/{users.uuid}`) and a display name where one is known (the `users` grant already allows first and last name); the referenced resources are not served by Clinical Care, so the reference dangles. *Alternative:* omit them. *Recommendation:* include them, and say so in the capability statement.
8. **Access:** the same sign-in as every other route (ADR 0075). A new gateway route sends `/fhir/Encounter`, `/fhir/Condition`, `/fhir/AllergyIntolerance`, `/fhir/Observation` and `/fhir/Immunization` to Clinical Care, beside the existing `/fhir/Patient` to Patient Management, and the gateway's protected-prefix list and its route test are extended so none of them can be reached without a session. **No bearer-token path** (ADR 0075 #7 still applies).
9. **Conformance:** map with the Firely SDK as Patient Management does. US Core validation runs **in the tests**, against sample output of each resource, not at runtime. The two vendored profile packages are about 15 MB; *recommendation:* move them to one shared location both services' tests read, rather than copying them a second time.
10. **A capability statement** (`GET /fhir/metadata`) is **not** in this slice; the five resources and the search parameters are documented in the OpenAPI document and in the slice note.

## Consequences

- A FHIR client signed in through the gateway can read a patient and the five clinical resource types for them.
- Observation ids for vitals differ from legacy's; documented, and a limitation for anyone comparing the two systems.
- Immunizations carry no `lastUpdated`; clients that sync by `_lastUpdated` cannot use it for them.
- The mapping code lives in Clinical Care's Api layer (as Patient Management's does), reading the existing read models, so the domain and its architecture rules are unchanged. Each resource is its own small, separately testable mapper.

## Order of work, once accepted

`Immunization` first (smallest: one table, one uuid, no stamp), then `AllergyIntolerance` and `Condition` (one table, two types), `Encounter`, and `Observation` last (the id and panel rules). Each lands with its mapper tests, a US Core conformance test, the search tests and the gateway route test.
