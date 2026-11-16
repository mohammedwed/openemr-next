# Clinical Care: layered review of slices 1 to 3

- **Date:** 2026-11-17. **Scope:** the Clinical Care service as merged in PR #77 (slices 1 to 3), and the PatientManagement relay and grant changes that shipped with it.
- **Method:** the playbook's six dimensions (`docs/microservice-playbook/08-quality-and-testing.md` section 4, MIG-12), each with **grounded evidence**: code read, tests run, probes executed against the merged code. A score is a conversation starter; the findings are the product.
- **Outcome in one line:** the review found **seven defects, all fixed and pinned by tests**: six of one kind (input the domain accepted but legacy's storage could not hold, which was a 500 or silent garbage: R-1 to R-5, R-7) and one tooling failure (R-6). **Seven further findings are tracked** (R-8 to R-14) and **three things were reviewed and deliberately left as they are**.

## 1. How the defects were found

The mechanical checks were clean (section 3). The defects came from one question asked of every column the service writes: **can the domain accept a value that this column cannot hold?** Reading the schema (`snapshots/db.sql`) against the domain's limits gave ten probes (eight distinct inputs; the date probe is three cases). They were turned into tests first and run against the *merged* code. **All ten failed**, which is what made them findings and not opinions:

| # | Input | Before | After |
|---|---|---|---|
| 1 | Encounter `date` of year 1, 999 or 9999 | **201**, stored | 400 (CC-17) |
| 2 | `onsetDate` of year 1 | **201**, stored | 400 (CC-17) |
| 3 | Encounter `providerId` of 3,000,000,000 | **500** | 400 |
| 4 | Vitals `providerId` of 3,000,000,000 | **201**, user NULL | 400 |
| 5 | Weight of `7e28 kg` | **500** (`System.Decimal` overflow inside the conversion) | 400 |
| 6 | Weight 1000 lb, height 0.001 in | **500** (derived BMI overflows `decimal(12,6)`) | 400 |
| 7 | Event with no `payload` | **500** | 400 |
| 8 | Event with `registeredAtUtc` of year 1 | **204**, stored | 204, stored as *unknown* |

A ninth (a missing-events-secret startup test) found no defect, only a gap in coverage, now closed. The one probe whose expectation I got wrong was #8: I expected a 400, but a nonsense *informational* timestamp should not dead-letter an otherwise valid patient event, so the better behaviour is the tolerant one.

## 2. Findings

`R-n` = review finding. Severity C/H/M/L.

| Id | Finding | Sev | Outcome |
|---|---|---|---|
| R-1 | `providerId` accepted up to `long.MaxValue`, but `form_encounter.provider_id` is an `int(11)`: 3e9 was a 500 (encounter) or a record with a NULL user (vitals). | H | **Fixed**: bounded to `int.MaxValue` in the domain and in both request contracts; same message |
| R-2 | Height had only a `> 0` bound, so a tiny height made the derived BMI overflow `decimal(12,6)`: a 500. | H | **Fixed**: floors added (height 5 in, weight 0.1 lb); the largest BMI any accepted input can produce is now 42,180, asserted by a test |
| R-3 | A number large enough to overflow `System.Decimal` threw `OverflowException` inside the unit conversion, *before* any bound was checked: a 500. | H | **Fixed**: a raw cap is checked before any arithmetic and reported as the ordinary out-of-bounds message |
| R-4 | An event with no `payload` property left an undefined `JsonElement` that throws on deserialize: a 500, which the relay reads as "retry", for an event that can never succeed. | H | **Fixed**: a 400 |
| R-5 | A nonsense `registeredAtUtc` was stored. It is informational. | L | **Fixed**: an implausible time is recorded as unknown; the patient is still recorded |
| R-6 | `generate-clinicalcare-openapi.sh` had **been broken since slice 3**: it did not supply the newly required `InternalEvents:Secret`, so the API refused to start (fail-fast working, tooling not updated). Nobody noticed because the hidden events endpoint does not change the snapshot. | M | **Fixed**; the drift guard then did exactly its job when the provider bound changed the contract |
| R-7 | Encounter and onset dates were unbounded; year 1 or 9999 was stored in a clinical record. | M | **Fixed**: 1900-01-01 to one year ahead (new rule CC-17) |
| R-8 | The relay sends `PatientRegistered` **including first and last name** over plain HTTP between containers. The consumer ignores the names. The event is a public contract written before any consumer existed. | M | **Tracked**: either TLS on the internal hop, or a `schemaVersion` 2 payload without names (EVT-5). Not decided here: it changes a contract. |
| R-9 | A genuine data conflict at the consumer (a different uuid claiming a held pid) is a **500**, which the relay treats as transient, so it blocks every later event for about 10 minutes (5, 10, 20, 40, 80, 160, 300 s) before dead-lettering. | M | **Tracked**: map the unique-key conflict to a permanent status (409) so it dead-letters at once |
| R-10 | Clinical Care writes `EncounterCreated` and `VitalsRecorded` to `clinical_care_outbox`, but **nothing relays or purges them**. They accumulate. Harmless until a second consumer exists. | L | **Tracked** with F-PM-3 (retention) |
| R-11 | A `date` sent with a `Z` or an offset is stored as its clock digits with the zone ignored (the field is documented as server-local time). | L | **Tracked**; document or reject offsets |
| R-12 | The same `INSERT ... SELECT` that resolves a provider's username and group appears three times (encounter forms row, vitals row, vitals forms row), as does the `'Default'` group literal. | L | **Tracked** (SOLID); see section 4 |
| R-13 | `EncounterValidationException` is thrown for things that are not encounters (`PatientReference`, `PageRequest`, the consumer). | L | **Tracked**: rename (`ClinicalCareValidationException`) in a mechanical change |
| R-14 | `GetVitalsUseCase.TryReference` is an `internal static` helper called by a sibling use case; it belongs on `EncounterReference`. | L | **Tracked** |

## 3. The six dimensions

### 3.1 Clean architecture
- **Evidence:** 31 architecture tests (inward-only layering, invariants). Read the references of every project; read the imports of the Application and Domain layers.
- **Clean:** `Domain` references nothing of ours; `Application` sees only ports; `Infrastructure` never references `Application` or `Api`; `Composition` references neither `Api` nor ASP.NET; the Api contracts are separate from the domain; wire unit spellings (`kg`, `degC`) live in one file (`UnitWire`).
- **Tracked:** R-13, R-14. A namespace cycle between `Domain.ValueObjects` and `Domain.VitalSigns` (`EncounterReference` throws the vitals exception): same assembly, no layer broken, cosmetic.
- **Reviewed, left as is:** the domain's error messages say `lb or kg`, `degF or degC`. That is the wire vocabulary, which DOM-14 says to keep out of the domain, but these strings *are* the API's error contract and the text clients read. Moving them would duplicate every message in the edge for no gain.

### 3.2 Documentation
- **Evidence:** scripted scan of every public type in the service for a missing doc comment: **0 undocumented**. Spot-read the rule citations.
- **Clean:** each ported rule cites the legacy file, and the docs say which version: the research was redone against the **running 8.0.0 container** after the source tree turned out to be 8.6.0-dev (every cited file differs), so the slice docs state which claims hold in 8.0.0.
- **Caveat found and applied consistently:** the "`sql_mode = ''` in legacy, strict here" difference is stated where it caused a bug (`forms.authorized`) and now in the seed-script and registry comments.

### 3.3 Security
- **Evidence:** read **all 32 SQL call sites** (every one uses EF's interpolated, parameterized API; the two raw uses are constant strings; no string concatenation into SQL anywhere); grep of every log statement; inspected the live service logs for parameter values and clinical text.
- **Clean:** parameters are masked (`@p0='?'`) in the EF command log and a search for known test names, notes and reasons found **zero** occurrences; no secrets in logs; constant-time secret comparison in one shared base class; two separate secret schemes, tested both ways (the gateway's secret cannot deliver events, the producer's cannot call the API); the events endpoint is hidden from OpenAPI and has no gateway route; no published port; least-privilege grants proven by tests (what the principal may *not* do), column-level where a table holds credentials.
- **Fixed:** R-1 to R-7 (limits belong in the domain, DOM-11).
- **Tracked:** R-8 (names over plain HTTP).
- **Inherited and still open, restated because the scope grew:** the gateway attaches the service secret to `/api/v1/encounters/**` **without checking who the browser user is** (ADR 0055, a standing decision). With slices 1 and 2 that path now carries encounter reasons and vital signs, which is more sensitive than the demographic data the decision was made for. There is no audit and no authorization policy (F-CC-1, F-CC-2, F-CV-3). This review does not change the decision; it records that the data behind it changed.
- **Reviewed, left as is:** the secret comparison returns early on a length mismatch, which reveals the secret's length. Same as PatientManagement's handler; the secrets are fixed-length hex generated by script.

### 3.4 DDD
- **Evidence:** read each aggregate and value object; the invariant test (no public constructor, no public setter) covers every rule-bearing type.
- **Clean:** `Encounter` and `Vitals` are valid from construction, take identity and the clock as inputs (the lesson PatientManagement's own review recorded), collect every error; read models are a separate project; reference data is passed in so the rules stay pure functions; the single write path per aggregate goes through its factory (path-per-test).
- **Observation:** both aggregates are create-only in these slices, so "aggregate" is thinner than in PatientManagement's insurance cluster. That is scope, not a flaw, and updates are explicitly deferred behind a concurrency token (LEG-10).

### 3.5 SOLID / design
- **Evidence:** scripted scan for symbols referenced only at their definition: none dead (the seven hits are extension-method classes). Read for duplicated orchestration.
- **Tracked:** R-12 (the user and group lookup, three copies), and the encounter and vitals registries share a retry-by-uuid shape. Worth a shared helper when a third writer appears; not worth one now.
- **Reviewed, left as is:** the two event-envelope implementations (PatientManagement builds one in its Application layer, Clinical Care builds one in Infrastructure). Design-set decision D4 says not to share types until a second context needs the same one; the shape is pinned by tests on both sides.

### 3.6 Resilience / fail-fast
- **Evidence:** fault tests (a registry that throws; a failure part-way through a transaction; a consumer that is down), a startup test for each required secret, and the live outage drill.
- **Clean:** every explicit transaction runs inside an execution strategy; retries are safe because each write is idempotent by uuid or by event id; a relay pass failing does not stop the host or lose a row (leases expire); a consumer outage backs off and recovers with no manual step; missing configuration fails startup, now tested for both secrets.
- **Fixed:** R-3, R-4 (a 500 means "retry me", so a request that can never succeed must be a 400); R-6 (tooling had not caught up with a new required setting).
- **Tracked:** R-9, R-10.

## 4. Reviewed and left as is
1. The wire vocabulary in domain error messages (3.1).
2. The early-exit length comparison on secrets (3.3).
3. Two envelope implementations (3.5).

## 5. Success measures (QLT section 9), re-baselined

| Measure | Before this review | After |
|---|---|---|
| Clinical Care tests (unit / architecture / integration) | 160 / 31 / 139 | 188 / 31 / 150 |
| Known 500s reachable by a malformed or extreme request | 4 (plus 4 inputs silently stored as garbage) | 0 for the inputs probed |
| Open findings of severity High | 0 recorded | 0 (the 4 High found, R-1 to R-4, were fixed) |

The honest limit of this review: it probed the inputs the schema made suspicious. It is not a fuzz test, and it did not cover the legacy UI (needs a login) or the paths that are not built (update, delete, billing).
