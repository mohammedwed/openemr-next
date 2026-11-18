# Clinical Care slice: FHIR AllergyIntolerance, Condition, Encounter and Observation (M5e, slices 2 to 5)

Decisions are in ADR 0079 (Accepted); Immunization, the first resource and the shared flow, is `24-fhir-immunization.md`. This note records the other four, and the one place where the build differs from the ADR. `_lastUpdated` search, built afterwards, is in section 6.

## 1. What was built

| Resource | Endpoints | Built from |
|---|---|---|
| `AllergyIntolerance` | `GET /fhir/AllergyIntolerance/{id}`, `GET /fhir/AllergyIntolerance?patient=...` | an allergy (`lists`, type `allergy`) |
| `Condition` | `GET /fhir/Condition/{id}`, `GET /fhir/Condition?patient=...[&category=problem-list-item]` | a problem (`lists`, type `medical_problem`) |
| `Encounter` | `GET /fhir/Encounter/{id}`, `GET /fhir/Encounter?patient=...` | an encounter |
| `Observation` | `GET /fhir/Observation/{id}`, `GET /fhir/Observation?patient=...[&category=vital-signs]` | a vitals set, as up to ten Observations |

The flow (read, search, Bundle, refusals, empty set for an unknown patient) is `FhirEndpointKit`, shared with Immunization; each resource adds only its mapper. Gateway routes and the verified-user middleware cover all five.

## 2. How each maps

| Resource | Rules |
|---|---|
| **Condition** | `clinicalStatus` `active`, or `resolved` when the problem has an end date or the resolved outcome; `category` `problem-list-item`; `code` from the diagnosis text with the title as `text`; `onset` and `abatement` as dates; `verificationStatus` only when the stored value is one of FHIR's; `note` from the comments |
| **AllergyIntolerance** | `clinicalStatus` `active`, or `inactive` with an end date; `code` as for Condition; `reaction` with the manifestation text and a severity of `mild`, `moderate` or `severe` (legacy's life-threatening and fatal map to `severe` and set `criticality` `high`; an unknown severity is left out, never guessed) |
| **Encounter** | always `finished` (Clinical Care does not track progress, and legacy says the same); `class` the stored ActEncounterCode; `type` text the visit category's name; `identifier` the encounter number; `period.start` with the server's offset; `reasonCode` text; `serviceProvider` the facility's name; `participant` the provider as `Practitioner/{uuid}` with a display name |
| **Observation** | one per measurement taken, never an empty one: blood-pressure panel (systolic and diastolic as components), heart rate, respiratory rate, body temperature (with its method), oxygen saturation, body weight, body height, BMI, waist and head circumference; category `vital-signs`; LOINC codes; UCUM units; `effective` the recorded time; `encounter` and `subject`; the set's note on each |
| **All four** | `meta.versionId` and a weak `ETag` equal to the JSON API's token; `meta.lastUpdated` from the record's stamp; the US Core profile in `meta.profile` |

Diagnosis text is `SYSTEM:code;SYSTEM:code`. ICD-10, ICD-9, SNOMED, RxNorm, LOINC, CVX and CPT are mapped to their FHIR code-system URIs; an unknown system becomes `urn:openemr:code-system:{name}`, so the code is carried; a part that is not `SYSTEM:code` is dropped, not guessed.

## 3. Where this differs from ADR 0079 (one place)

| # | The ADR said | What was built | Why |
|---|---|---|---|
| D1 | Vitals Observation ids are a name-based UUID | `{vitalsUuid}-{measurement}`, for example `…-body-weight` | A hash cannot be read back: `GET /Observation/{id}` has to find the vitals set from the id. This is still derived, deterministic and write-free, still differs from any id legacy minted, and is what ADR decision 3 was after. |

## 4. Limitations

| # | Limitation | Why accepted |
|---|---|---|
| L1 | An Observation search reads the patient's **most recent 1000 vitals sets** and counts and pages over their measurements. Older sets are out of FHIR's reach (the JSON API still lists them). | The database cannot count measurements; bounded rather than unbounded. |
| L2 | A practitioner or facility reference dangles (no `Practitioner` or `Organization` is served). | ADR 0079 #7, the user's choice. |
| L3 | Times carry the server's UTC offset. | The same reading as the JSON API. |
| L4 | A vitals set with no recorded time has Observations with no `effective[x]`, which US Core requires. | Legacy rows without a date; not worth inventing one. |
| L5 | Only problem-list conditions: no encounter diagnoses or health concerns. | Clinical Care holds nothing else. |
| L6 | No `$validate`, no capability statement, no writes. | ADR 0079 #1 and #10. |

## 5. Tests

`FhirClinicalResourceTests` (45; run and green, 103 with the Immunization, route and OpenAPI tests) seeds through the JSON API, and **validates every resource served against its US Core profile**; the Observation test validates each of the ten measurements against its own profile. It also pins: the status, code, onset and note mappings above; the version and `lastUpdated`; search returning only the patient's own records; paging over observations with `next` followed to the end and no entry twice; an id for a measurement not taken, malformed, or unknown being a 404; deleted vitals and another patient's never served; the single category accepted bare or with its system and any other refused; the refusals (`date`, `_lastUpdated`, `_count=0`, nothing to search on); an unknown patient being an empty set; and 401 without the secret or a verified user on every route. Gateway proxy tests pin each path to Clinical Care.

## 6. `_lastUpdated` search (M5e, ADR 0079 #6)

Offered on `Encounter`, `Condition`, `AllergyIntolerance` and `Observation`, the four with a stamp; **`Immunization` still refuses it**, because its table has none (ADR 0072).

| Value | Means |
|---|---|
| a year, a month, a day, or a date and time to the minute or second | the **range** its precision gives: `2026-03` is the whole month |
| no prefix, or `eq` | the stamp is inside the range |
| `gt` | the stamp is at or after the range's **end** (so `gt2026-03-15` excludes a stamp on the 15th) |
| `ge` / `lt` | at or after the range's start / before its start |
| `le` | before the range's end (so `le2026-03-15` includes the 15th) |
| `ne`, `sa`, `eb`, `ap`, or anything that is not a date | a 400 that says what is supported |
| two `_lastUpdated` parameters | both must hold: a range. A third is a 400. A range that can hold nothing is an empty set, not an error |

A value with a zone (`Z`, `+02:00`) is an instant converted to the server's local time, which is what the stamps are in; one without is taken to be local. A `+` that reached the service as a space is read as the plus it was. A search by `_id` is held to the window too. The window is part of the Bundle's `self` and `next` links, and `total` counts what matches the window.

**How it is applied:** in the database for encounters, problems and allergies (the window rides on the page request, so no reader signature changed); for Observations, after the read, to the patient's most recent 1000 vitals sets (limitation L1: a window older than those finds nothing).

**Tests:** `FhirDateSearchTests` (pure: ranges, prefixes, zones, refusals, the window's contains and intersect) and `FhirLastUpdatedTests` (each of the four resources seeded at known stamps, one of them exactly on a boundary; 14 queries per resource whose expected set is computed from the stamps by the FHIR rule; ranges; empty ranges; the total; paging through a window; `_id` held to the window; refusals; Immunization still refusing).
