# Schedule: M2 + M3 by Dec 31, 2026

Scope decision: target M2 (schema inventory) and M3 (gateway and token
validation) by Dec 31. M4–M6 are explicitly deferred past this date, per the
roadmap's own guidance ("If time runs short, reduce M5 and M6 rather than
leaving M0–M4 half-finished") — M4 itself is also pushed out here because
M1's actual pace (~5 weeks for the identity-bridge hardening alone) doesn't
support M2+M3+M4 in 7 weeks of part-time solo work.

Today is Nov 12, 2026. Dec 31 is 7 weeks out. Two of those weeks overlap US
holidays (Thanksgiving week, Christmas/New Year week) and are budgeted as
reduced-capacity/buffer, not full work weeks.

Dates are not promises (per the roadmap's own framing) — this is a plan to
revise against, not a commitment to defend.

## Week-by-week

| Week | Dates | Focus | Deliverable |
|---|---|---|---|
| 1 | Nov 12 – Nov 19 | M2: map every table in slice scope to a bounded context | Table → context map drafted |
| 2 | Nov 19 – Nov 26 | M2: list cross-context tables and FKs, decide reference ID / duplicated data / API call per FK. **Thanksgiving week — reduced capacity assumed.** | Cross-context list + decisions, partial |
| 3 | Nov 26 – Dec 3 | M2: finish cross-context decisions, record legacy anti-patterns as ADR input, close out M2 | **M2 exit: decision gate — confirm Patient–Encounter boundary** |
| 4 | Dec 3 – Dec 10 | M3: YARP gateway scaffold, routes to AuthFacade, Compose entry for the gateway | Gateway running in Compose, routing to AuthFacade |
| 5 | Dec 10 – Dec 17 | M3: token validation at the gateway; BFF proxy adds the gateway cluster (per ADR 0030) | Token validation live at gateway |
| 6 | Dec 17 – Dec 24 | M3: ADR — AuthFacade as permanent bridge vs. replaced by a dedicated identity service. **Christmas Eve — reduced capacity assumed.** | ADR drafted |
| 7 | Dec 24 – Dec 31 | Buffer/catch-up from weeks 2 and 6; close M3 | **M3 exit: identity architecture ADR merged** |

## Risks specific to this schedule

- **M2 can expand** (already flagged in the roadmap) — if the table/FK
  inventory runs past week 3, M3 start slips and week 7's buffer absorbs it
  first; M4 start slips further, beyond this schedule's Dec 31 horizon.
- **Two holiday weeks in a 7-week window** is a real capacity hit for a
  solo part-time effort — weeks 2 and 6 are deliberately light, with week 7
  as the main buffer.
- **No changelog entry since Nov 2** (10 days before this schedule was
  written) — if that gap reflects a sustained drop in available time rather
  than a pause, this schedule should be revisited now rather than at week 3.

## Out of scope for this date

M4 (Patient Management service), M5 (Clinical Care service), M6 (portfolio
polish) — unscheduled. Re-plan these once M3's exit criteria are met and
actual pace through M2+M3 is known.
