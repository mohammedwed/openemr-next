# 0065. Vitals are stored in US units, and the API takes and returns explicit units

- **Status:** Accepted
- **Date:** 2026-11-16
- **Builds on:** [0064](0064-clinical-care-shares-the-legacy-database-and-coexists-with-legacy-encounter-writers.md), `docs/clinical-care/02-record-vitals-slice.md`
- **Rule(s):** DOM-6, DOM-9, DOM-12, DOM-14, DOM-15, LEG-2, API-2 (playbook `docs/microservice-playbook/`)

## Context

Clinical Care slice 2 records an encounter's vital signs into legacy's `form_vitals` table. Reading the code the
stack actually runs (**OpenEMR 8.0.0**, in the container, not the newer source tree) shows three facts that decide
how units must work:

1. **The database always stores US units**: weight in pounds, height and circumferences in inches, temperature in
   degrees Fahrenheit. `VitalsService::createResultRecordFromDatabaseResult` converts to metric only **on read**,
   and only when the `units_of_measurement` global is 2 or 4 (here it is 1). Nothing converts on write.
2. **The unit a caller meant is not in the data.** The form posts a bare number. A weight of `70` is 70 lb or 70 kg
   depending on a global setting that the receiving code never consults. Storing it unconverted would silently
   record 70 kg as 70 lb, which is a clinically wrong value that looks valid.
3. **A zero means "not recorded".** The columns are `decimal(12,6) DEFAULT 0.000000`; legacy reads a `0` back as
   absent (`if ($val != 0)`), and a genuine zero measurement does not exist for any of these quantities.

## Decision

1. **Storage stays in US units.** Clinical Care writes pounds, inches and degrees Fahrenheit, exactly as legacy does,
   so legacy screens, reports, and its FHIR API show the right numbers (parity).
2. **Every unit-bearing value crosses the API with an explicit unit.** A request gives `{ "value": 70, "unit": "kg" }`,
   not `70`. Accepted units: weight `lb` or `kg`; height, waist and head circumference `in` or `cm`; temperature
   `degF` or `degC`. An unknown or missing unit is a 400, never a guess. This is **(new)**: legacy has no unit on the
   wire.
3. **Conversion is a pure, cited domain rule** (`UnitConversion`) using the factors legacy's `MeasurementUtils`
   uses (`0.45359237`, `2.54`, `5/9`), rounded to the column's six decimal places. It runs once, on the way in.
4. **Reads return the stored (US) unit with its label by default**, and `?units=metric` converts for display with the
   same rule, mirroring what legacy does for `units_of_measurement` 2 and 4. The domain never stores metric.
5. **An absent measurement is `null` in the model and `0` in the column** (DOM-9). Reading treats a stored `0` as
   absent; writing never stores a zero for something the caller did not supply.
6. **BMI is derived, never accepted.** The API has no BMI input. It is computed from the stored weight and height
   (`weight_lb / height_in² × 703`) and its category by `BmiCategory`'s thresholds, as legacy's form controller does.

## Consequences

- A caller cannot cause the 70-lb-for-70-kg error: the unit is mandatory and converted.
- The wire contract is richer than legacy's. The generated client carries a small `Quantity` type.
- Round trips are not exact: 70 kg is stored as 154.323584 lb and reads back as 70.000000 kg only to six decimals.
  Tests assert within that precision, not equality.
- Slice 2 does not replace legacy's metric-persisting mode (`units_of_measurement` 2): that setting changes what the
  UI shows, not what the table holds, so nothing here depends on it. If a deployment ever changed what the table holds,
  every legacy reader would break first.

## Alternatives considered

- **Accept bare numbers in US units only.** Simplest and closest to legacy, and exactly the unit-mistake trap above.
  Rejected.
- **Accept bare numbers in the deployment's configured units.** Makes the API's meaning depend on a legacy global
  this service must not read, and changes silently when an admin edits that setting. Rejected.
- **Store metric.** Breaks every legacy reader. Rejected.
- **Accept a caller-supplied BMI.** Legacy overrides it whenever weight and height exist, so it only matters when
  they do not. A hand-typed BMI with no inputs is not a measurement this service wants to originate. Rejected for
  now; revisit if a clinical need appears.

## Revisit when

- A second consumer needs vitals in another unit system, or LOINC/UCUM codes (then `Quantity` grows a code system).
- Legacy is upgraded and its storage convention changes (the 8.6 source tree has a `VitalsStorageUnits` class that
  does not exist in 8.0.0; read it before upgrading).

## Related decisions

0064; `docs/clinical-care/02-record-vitals-slice.md`.
