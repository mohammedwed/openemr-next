# Clinical Care M5d slice 4: encounter lookups

- **Milestone:** ROADMAP M5d, slice 4. **Closes the API half of** decision 10 in [patient-view-pending-decisions.md](../patient-view-pending-decisions.md) (lookups for the encounter form).
- **Endpoints:** `GET /api/v1/lookups/encounter-classes`, `GET /api/v1/lookups/facilities`, `GET /api/v1/lookups/calendar-categories`. **Status:** built and wired into the WebApp's create-encounter dialog; suites green (section 4). Not browser-verified (needs a signed-in user).

## 1. What it does

Creating or updating an encounter takes a class code, a calendar category id and a facility id, and nothing used to return the valid values: the WebApp offered a fixed list of 11 classes and plain number fields. These three read-only lists return what legacy has configured, so a caller can offer a choice and send a value the encounter endpoints accept.

## 2. Rules

| Id | Rule | Legacy source | Where |
|---|---|---|---|
| LK-1 | **Classes** are every `_ActEncounterCode` option in `seq` order, with the default marked and no `activity` filter (legacy filters on it in neither its lookup nor its validation). The same list the create and update validation already uses. | `ListService::getOptionsByListName` | `EfEncounterCodeLists` |
| LK-2 | **Facilities** are the non-inactive ones, by name then id, each with its default place-of-service code and whether legacy marks it a service location. A facility with no name reads as an empty name. | `facility` | `EfEncounterLookups` |
| LK-3 | **Categories** are the ones legacy's own visit form offers, restricted to patient visits: active, `pc_cattype = 0`, not `no_show`, in `pc_seq` then id order. Types 1 and 2 are provider and clinic events; type 3 is therapy groups, a feature this service does not offer (legacy's form shows type 3 only when it is enabled). | `C_EncounterVisitForm::getVisitCategoriesForTemplate` | `EfEncounterLookups` |
| LK-4 | Every list is **bounded** at 500 rows, so a misconfigured install cannot make a lookup unbounded (API-6). Empty configuration is an empty list, not an error. | none | `EfEncounterLookups` |
| LK-5 | **Providers are deliberately not offered.** They are staff names, and the acting provider should come from the session once identity is forwarded (M5b phase 1b). Offering names over an API that is reachable without sign-in today would widen that open finding. | decision 10 | not built |
| LK-6 | The database user gets `SELECT` on the extra columns the lookups read and nothing more: `inactive` and `service_location` on `facility` (still no address, phone or tax columns), and six columns of `openemr_postcalendar_categories` (not the colour, description or recurrence columns). No write privilege on either. | none | seed script |

## 3. Known limitations

| # | Limitation | Why accepted |
|---|---|---|
| L1 | Creating an encounter still accepts **any** category id of 1 or more: it is not validated against the category table (as before). A lookup narrows what the UI offers, not what the API accepts. | Existing behaviour; validating needs a decision on legacy categories of other types |
| L2 | **The WebApp falls back** to the fixed class list and plain id fields when any lookup cannot be loaded, with a warning, so an outage of a convenience cannot stop an encounter being recorded. With the lookups, the class defaults to legacy's default, the category to the first configured one, and the facility to the only one when there is exactly one (as legacy does). | Deliberate |
| L4 | **The gateway had no route for these (or for immunizations).** `/api/v1/immunizations` and `/api/v1/lookups` were served by Clinical Care but 404 through the gateway, and nothing caught it because the gateway's tests run only against a live stack. Fixed, with `GatewayRoutesTests` in the Clinical Care suite comparing every API prefix in the OpenAPI document with the gateway's route list (it fails without the fix). | Found while wiring the WebApp |
| L3 | No providers list (LK-5). | Needs identity |

## 4. Tests

| Layer | What it pins |
|---|---|
| Integration (HTTP, real MariaDB) | Classes in `seq` order with the default marked; facilities active-only by name with POS and service-location flag; categories exclude `no_show`, provider and clinic events, therapy groups and inactive ones, in sequence order; empty configuration gives empty lists; empty names read as empty; **the 500-row bound**; **what the lookups offer is accepted by creating an encounter**; 401 without the gateway secret; no providers route |
| Integration (grants) | The facility's address, phone and tax columns and the categories' colour, description and recurrence columns are unreadable; neither table is writable |
| OpenAPI | All three are documented as GET-only |
