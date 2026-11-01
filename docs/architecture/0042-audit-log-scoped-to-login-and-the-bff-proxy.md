# 0042. The audit trail covers login outcomes and the BFF proxy, not `/me`

- **Status:** Accepted
- **Date:** 2026-11-01

## Context

ROADMAP.md's M1 listed "audit log of login outcomes and `/me` access, with no credentials or tokens in the
log" as remaining. `/me` is what the dashboard calls on every page load to check whether it is signed in
(`Home.razor`'s `OnInitializedAsync`) — logging it would give a timeline of when a session was used, from
where, useful during incident response if a session cookie is stolen.

That value is real but smaller than it sounds: `/me` returns no PHI, just an identity check (`subject` and
`name`), and fires on every dashboard load — high-volume compared to login, which happens once per session.
The thing a HIPAA-style audit trail actually cares about — who touched what OpenEMR data, when — flows
through the BFF proxy (`/bff/openemr/...`, ADR 0029), not `/me`. Logging `/me` would add volume without
adding much a BFF-proxy log doesn't already cover for the sessions that actually do anything.

## Decision

1. **The audit trail covers two things: login outcomes (`/callback`) and BFF proxy access.** Not `/me`.
2. **`AuditEvents`**, a small `[LoggerMessage]`-generated static class (`Api/Audit/`), with three structured
   entries: `LoginSucceeded(subject)`, `LoginFailed(reason)`, `ProxyAccess(subject, method, path, statusCode)`.
   A separate marker type, `AuditLog`, fixes the logger category name
   (`OpenEmrNext.AuthFacade.Api.Audit.AuditLog`) so it stays stable and filterable regardless of which file
   calls it from.
3. **`LoginFailed`'s reason is a fixed internal label** (`"rejected"`), never OpenEMR's or OpenIddict's own
   error text — the same "generic reason, never leak upstream text" principle `/callback`'s own
   browser-facing `?error=` code already follows (ADR 0026), now also applied to what reaches an operator's
   log, not just what reaches the browser.
4. **The BFF proxy's entry is a YARP response transform**, logged after the call completes (or fails) with
   the subject from the session's claims, the method and the *browser-facing* path (`/bff/openemr/...`, not
   OpenEMR's own rewritten one — what the caller actually asked for), and the resulting status code. Never
   a request or response body, never the bearer token the request transform right next to it attaches.
5. **No audit entry for an unauthenticated BFF proxy attempt.** Authentication middleware already refuses
   those with 401 before the proxy's own route runs; there is no subject to log, and "who accessed what"
   is not the question an anonymous 401 answers.

## Consequences

- An operator can filter straight to `OpenEmrNext.AuthFacade.Api.Audit.AuditLog` for every login outcome
  and every authenticated call to OpenEMR's data, without wading through the much higher-volume `/me`
  traffic or framework/library log noise.
- Verified against the live Compose stack: driving `/callback` with a mismatched state through the real
  gateway produced a real `Login failed: rejected` entry under that exact category. The BFF proxy's entry
  and the "never logs the access token" guarantee are covered by `AuditLogTests` against a fake OpenEMR
  server instead — reaching the BFF proxy live needs a signed-in session, which needs real OpenEMR
  credentials unavailable in this session (the same limitation ADR 0038/0040/0041 each already hit).
- `/me` access itself is still unaudited. If the narrower, session-activity-timeline value named in Context
  turns out to matter in practice, it is a small, separate addition — this decision does not foreclose it,
  it just did not choose it now.

## Alternatives considered

- **Audit `/me` as originally listed in ROADMAP.md.** Rejected for now — see Context. The BFF proxy is a
  strictly more relevant signal for the audit question this item exists to answer, at a fraction of the
  volume.
- **Log the full request/response body at the BFF proxy boundary**, for a richer audit record. Rejected:
  that is exactly what `RequestLoggingTests` already guards against happening anywhere in this service, and
  reopening it for "audit purposes" would still mean real patient data sitting in a log file.
- **A dedicated audit sink (database table, external log shipper) instead of `ILogger`.** Rejected as
  unnecessary for now: this is a local dev stack (ADR 0020) with no shipping/retention story for *any* log
  yet; a structured, filterable category is the smallest thing that is still genuinely useful, and a real
  deployment's own log pipeline decides where `ILogger` output ultimately lands.

## Related decisions

Answers the ROADMAP.md M1 item partially, narrowed to the BFF proxy instead of `/me`. Guards ADR 0029's BFF
proxy and ADR 0026's login flow. Follows the same "generic reason, never leak upstream text" principle ADR
0026 established for the browser-facing `?error=` code, applied here to what reaches a log instead.
