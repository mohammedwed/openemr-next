# 0030. The BFF proxy forwards to the future gateway, not to each backend service directly

- **Status:** Accepted (records a decision for when M3 starts; no code changes)
- **Date:** 2026-10-31
- **Builds on:** [0029](0029-yarp-proxies-browser-sessions-to-openemrs-api.md)

## Context

ADR 0029 built the BFF proxy with exactly one route and one cluster: `/bff/openemr/{**catch-all}` →
`OpenEmr:BaseUrl`. Its Consequences left a question open: once Patient Management (M4) and Clinical Care (M5)
exist, how does a logged-in browser session reach *them* through the same proxy — does the proxy grow a
second route/cluster per service as each is built, or does something else sit between it and them?

ROADMAP.md's M3 milestone already answers part of this, independently of the proxy: a YARP gateway "routing
to AuthFacade and to the new services," doing "token validation at the gateway." That gateway is squarely
about the new services (Patient Management, Clinical Care) — it has nothing to do with OpenEMR, which stays
the external, already-wrapped legacy system ADR 0001/0012 established, reached the way ADR 0029 already
built. The open question was really about where *this proxy's* request lands once it needs to reach one of
those new services, not about OpenEMR.

Two options were weighed:

1. **Grow this proxy's own route table.** Add one route/cluster pair per new service as each is built
   (`/bff/patients/{**catch-all}` → Patient Management, `/bff/clinical/{**catch-all}` → Clinical Care, and so
   on). Simple to extend incrementally, but it turns AuthFacade into a general-purpose router that accretes a
   path per service — exactly the role ADR 0001/0002/0007 deliberately kept it out of (a narrow token broker,
   not an API gateway), and it pre-empts M3's own design by solving gateway-shaped routing here first.
2. **Add a single cluster pointed at the gateway**, once M3's gateway exists, as the one hop for anything
   beyond OpenEMR. The gateway does the per-service fan-out and its own token validation (exactly what M3 was
   already scoped to do); this proxy's job stays "attach the token, forward one hop," regardless of how many
   backend services ever exist behind the gateway.

## Decision

1. **Go with option 2.** This proxy will have at most two clusters, ever: the existing direct one to OpenEMR
   (ADR 0029, unaffected by this decision — OpenEMR is the wrapped legacy system, not one of "the new
   services" M3 describes), and a second one added when M3's gateway exists, pointed at the gateway as a
   single upstream for every other backend call a browser session needs to make.
2. **No code changes accompany this ADR.** There is no gateway and no second service yet (M3, M4 and M5 have
   not started per ROADMAP.md), so this records the decision for when that work begins rather than building
   ahead of it.
3. **The gateway owns per-service routing and its own token validation**, as ROADMAP M3 already states. This
   proxy does not duplicate that logic by learning about Patient Management or Clinical Care individually —
   it only ever needs to know about the gateway.

## Consequences

- AuthFacade's BFF proxy stays a narrow, two-destination component indefinitely: OpenEMR directly, and (once
  built) the gateway for everything else. Adding a third backend service never means a third cluster here.
- M3's design must account for being reached this way: a request arriving at the gateway may already carry
  the attached OpenEMR bearer token from this proxy's transform (ADR 0029), which the gateway's own token
  validation needs to either reuse or re-derive its own authorization decision from — left for M3's own ADR
  to settle, not resolved here.
- This sharpens, rather than answers, ROADMAP M3's existing open item ("ADR: whether AuthFacade stays as the
  permanent legacy bridge or is replaced by a dedicated identity service") — this proxy's OpenEMR cluster
  continues either way; only the routing to the *new* services goes through the gateway.

## Alternatives considered

- **Grow this proxy's own route table per service** (option 1 above). Rejected: it would make this proxy the
  de facto gateway by accretion, one service at a time, pre-empting a decision ROADMAP M3 already reserved
  for its own milestone.

## Related decisions

Builds on [0029](0029-yarp-proxies-browser-sessions-to-openemrs-api.md), whose OpenEMR cluster is unaffected.
Feeds ROADMAP M3, which now has an answer, decided ahead of time, for how the BFF proxy and the gateway
relate once M3 starts.
