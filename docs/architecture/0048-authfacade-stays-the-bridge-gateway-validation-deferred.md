# 0048. AuthFacade stays the identity bridge; the gateway's token validation is deferred

- **Status:** Accepted
- **Date:** 2026-11-13
- **Builds on:** [0029](0029-yarp-proxies-browser-sessions-to-openemrs-api.md), [0030](0030-bff-proxy-forwards-to-the-gateway-not-each-service.md), [0033](0033-gateway-fronts-webapp-and-authfacade.md), [0047](0047-the-identity-bridge-design.md)

## Context

ROADMAP.md's M3 names two open items in one sentence: a YARP gateway "routing to AuthFacade and to the
new services," doing "token validation at the gateway," plus a separate decision gate — "ADR: whether
AuthFacade stays as the permanent legacy bridge or is replaced by a dedicated identity service." ADR 0030
already noticed these two items are entangled and explicitly left both for "M3's own ADR" to settle. This
is that ADR.

**There is no bearer token on the browser path to validate.** Since ADR 0035, the browser never holds one;
since ADR 0027/0043/0044, the real OpenEMR access and refresh tokens live only in AuthFacade's server-side
session ticket store, referenced by an opaque cookie. The one bearer token that exists anywhere in the
running system is the one AuthFacade's own BFF proxy attaches, server-side, to its forwarded call to
OpenEMR (ADR 0029) — and that call goes directly from AuthFacade to OpenEMR today, never through the
gateway. The gateway, as built (ADR 0033), deliberately "does nothing: no auth decisions, no token
handling" — a comment in `Program.cs` that was correct when written and remains correct after this ADR.

So "token validation at the gateway" was written against a system that no longer matches it. Before
deciding how to build it, this ADR asks whether there is anything concrete to validate yet.

## Decision

1. **AuthFacade remains the permanent identity bridge for OpenEMR.** No dedicated identity service is
   planned or built in this roadmap's current scope. OpenEMR continues to own every account, password,
   and ACL; AuthFacade continues to hold none of its own (ADR 0047). This follows directly from the
   roadmap's own guiding principle — "prefer a module over a service unless independent deployment or
   scaling pays off" — and from "evidence-driven": nothing in M2–M6 as scoped today needs a capability
   AuthFacade's current narrow bridge doesn't already provide correctly.
2. **This decision is scoped to the current roadmap only.** It says nothing about the separately-floated,
   not-yet-scheduled idea of native OpenEmrNext authentication (accounts that exist outside OpenEMR). If
   and when that is actually planned, it gets its own ADR, informed by whatever that need turns out to
   require — not a consequence of this one.
3. **The gateway's token validation is deferred, not built, in M3.** The gateway keeps doing exactly what
   ADR 0033 built it to do: route by path, terminate TLS, take no auth decisions. It stays that way until
   one of two concrete triggers gives it an actual bearer token to validate:
   - The BFF proxy gains a second cluster pointed at the gateway once a second backend service exists
     (ADR 0030), **and** that path is chosen to route through the gateway rather than staying a direct
     hop — at which point the gateway is the first thing to see whatever token the proxy attaches.
   - A future caller that isn't the browser (a native client, a service-to-service caller) authenticates
     with a bearer token the gateway is the first component to receive.
   Building validation logic now means writing it against a guessed contract no real caller exercises —
   the same evidence-driven concern that already governs M2.
4. **M3's other blocked items are unaffected.** Routes to Patient Management and Clinical Care, and the
   BFF proxy's gateway cluster, stay correctly blocked on M4/M5 actually existing. Nothing here changes
   that sequencing.

## Consequences

- ROADMAP M3's "ADR: AuthFacade permanent bridge vs. dedicated identity service" item is closed.
- ROADMAP M3's "token validation at the gateway" line is reinterpreted, not deleted: deferred until one of
  the two named triggers occurs, rather than attempted against no real caller.
- `AuthFacade`'s name stays as-is, settling the open question behind that prior naming discussion: it
  accurately describes a permanent, narrow OpenEMR-specific bridge, not a placeholder for something more
  generic.
- The `Program.cs` comment in the gateway project ("no auth decisions, no token handling") describes the
  durable design, not a temporary state M3 was going to change — it does not need updating for this ADR,
  but a future PR implementing validation (once a trigger fires) will need to update it then.
- M3's own exit criterion — "record the identity architecture decision" — is met by this ADR being
  accepted, independent of whether gateway validation code exists yet.

## Alternatives considered

- **Build a dedicated identity service now, ahead of any proven need.** Rejected: nothing in this
  roadmap's scope needs a capability the current bridge lacks; this would contradict "prefer a module over
  a service unless independent deployment or scaling pays off" and "evidence-driven."
- **Implement gateway token validation now, against a guessed contract** (e.g., reusing OpenEMR's own JWT
  shape, or treating the BFF proxy's attached token as if it already flowed through the gateway). Rejected:
  no real caller exists yet to validate the guess against, and a contract written against a guess is a
  rewrite waiting to happen once a real trigger clarifies what is actually needed.

## Related decisions

Builds on [0029](0029-yarp-proxies-browser-sessions-to-openemrs-api.md),
[0030](0030-bff-proxy-forwards-to-the-gateway-not-each-service.md),
[0033](0033-gateway-fronts-webapp-and-authfacade.md), [0047](0047-the-identity-bridge-design.md). Closes
ROADMAP.md's M3 decision gate ("record the identity architecture decision").
