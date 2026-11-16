# Changelog 2026-11-17

## Reviewed and fixed — layered review of Clinical Care slices 1 to 3

Full record: `docs/clinical-care/04-layered-review.md`. The review asked one question of every column the service writes: *can the domain
accept a value this column cannot hold?* Ten probes were written first and run against the merged code; **all ten failed**.

- **Fixed (seven defects):**
  - Provider id accepted up to `long.MaxValue` but the column is `int`: a 500 for an encounter, a NULL-user record for vitals (R-1).
  - A tiny height made the derived BMI overflow `decimal(12,6)`: a 500. Height and weight now have floors, and the worst accepted BMI is
    asserted to fit (R-2).
  - A huge number overflowed `System.Decimal` inside the unit conversion before any bound was checked: a 500 (R-3).
  - An event with no `payload` was a 500, which the relay reads as "retry", for an event that can never succeed. Now a 400 (R-4).
  - A nonsense `registeredAtUtc` is stored as unknown instead of as garbage; the patient is still recorded (R-5).
  - `generate-clinicalcare-openapi.sh` had been broken since slice 3 (it did not supply the newly required events secret) (R-6).
  - Encounter and onset dates of year 1 or 9999 were stored in a clinical record. Now bounded to 1900 up to a year ahead, rule CC-17 (R-7).
  - Oxygen saturation is rounded to the column's two places so the value reported is the value held.
- **Tracked (seven):** patient names sent over plain HTTP between containers (R-8); a consumer data conflict is a 500 that blocks the relay
  about 10 minutes before dead-lettering (R-9); Clinical Care's own outbox is never relayed or purged (R-10); offsets on dates are ignored
  (R-11); the provider lookup SQL appears three times (R-12); `EncounterValidationException` is thrown for non-encounter things (R-13);
  a helper on the wrong class (R-14).
- **Reviewed and left as is (three):** wire vocabulary in domain error messages (they are the API's error contract), the early-exit length
  comparison on secrets, and two envelope implementations (design-set decision D4).
- **Clean on inspection:** all 32 SQL call sites are parameterized; no parameter values or clinical text in the logs; no undocumented public type;
  no dead code.
- **Tests:** Clinical Care unit 188, architecture 31, integration 150, all green. The OpenAPI drift guard fired when the provider-id bound
  changed the contract, as designed, and the snapshot and client were regenerated (two added `maximum` lines).
- **Restated, not changed:** the gateway attaches the service secret without checking who the browser user is (ADR 0055). With slices 1 and 2 that
  path carries encounter reasons and vital signs, which is more sensitive than the data the decision was made for.
