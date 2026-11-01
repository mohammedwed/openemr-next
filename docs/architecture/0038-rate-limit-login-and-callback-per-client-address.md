# 0038. The browser login flow is rate limited per client address

- **Status:** Accepted
- **Date:** 2026-11-01
- **Resurrects:** [0018](0018-rate-limit-the-login-endpoint-per-client-address.md)

## Context

`GET /oidc/login` and `/callback` (ADR 0026) are AuthFacade's only remaining authentication entry point since
ADR 0035 removed the password grant along with the rate limiter ADR 0018 had built for it (`RateLimiting/`,
the whole folder, deleted outright because nothing called the policy it guarded anymore). Nothing stops a
client from driving the browser flow as fast as the network allows: `/oidc/login` builds an authorize URL and
sets a correlation cookie on every call, and `/callback` calls out to OpenEMR's token and JWKS endpoints on
every call with a `code` attached, win or lose. OpenEMR has its own `login.php` lockout, but it is per
account, and it does not protect AuthFacade's own resources or OpenEMR's token endpoint from a flood routed
through the facade.

ADR 0018's design (ASP.NET Core's built-in rate limiter, a fixed window per client address, refuse at once
rather than queue) was sound; it was removed only because its one caller was removed with it. Nothing about
moving from the password grant to the browser flow changes any of that reasoning.

## Decision

1. **The limiter returns**, `RateLimiting/LoginRateLimitOptions.cs` and
   `RateLimiting/RateLimitingServiceCollectionExtensions.cs`, same shape as ADR 0018: ASP.NET Core's built-in
   limiter (no new package), a fixed window per client address bound from `LoginRateLimit:PermitLimit` /
   `LoginRateLimit:Window` (defaults 10 requests per minute), validated at startup (`ValidateOnStart`), no
   queueing, 429 with a `Retry-After` header on refusal.
2. **One policy, `"login"`, required by both `/oidc/login` and `/callback`** (`.RequireRateLimiting(...)` in
   `OpenIddictClientEndpoints`), not two separate limiters. The two routes are one flow from one browser; a
   client that spends its budget on `/oidc/login` should not get a second, independent budget on `/callback`
   just by moving to the next step. `AddLoginRateLimiting()` is chained into `AddAuthFacadeHttpDependencies`
   and `app.UseRateLimiter()` runs first in `UseAuthFacadeHttpMiddleware`, before authentication — an
   over-limit request is refused before either handler runs, and before `/callback` would call out to
   OpenEMR at all.
3. **The rejection body drops the old `ErrorResponse` shape.** That type was deleted with ADR 0035 along with
   the JSON token endpoint it belonged to; `/oidc/login` and `/callback` are browser-navigation endpoints, not
   a versioned JSON API, so resurrecting a whole response-contract type for one error code was not worth it.
   The 429 body is a bare anonymous JSON object, `{"error":"too_many_requests"}` — enough for anything that
   does inspect it, nothing more.
4. **`/api/v1/me` stays unlimited**, unchanged from ADR 0018's reasoning: it checks a session cookie locally
   and never drives the flow this limiter protects.

## Consequences

- A client that sends too many requests to `/oidc/login` or `/callback` in a minute is refused — refused
  before `/callback` ever calls OpenEMR's token or JWKS endpoint — without reaching OpenEMR at all.
- A legitimate user behind a shared address (an office NAT) can hit the limit when many people log in
  together, same as ADR 0018; the limit is per address, so it cannot tell them apart.
- The limiter keys on `RemoteIpAddress`, unchanged from ADR 0018: behind a reverse proxy, every request looks
  like it came from the proxy until forwarded headers are configured and trusted.
- In-memory counters, unchanged from ADR 0018: a restart resets them, and a multi-instance deployment allows
  up to N times the configured limit (ADR 0028 already names the broader single-instance assumption this
  falls under).

## Alternatives considered

- **Two independent policies, one per route.** Rejected: it would let an attacker effectively double their
  budget by alternating routes, defeating the point of limiting the flow as a whole.
- **Resurrect `ErrorResponse` for the 429 body.** Rejected: that type existed for the versioned JSON token
  endpoint ADR 0035 removed; these two routes are plain browser redirects, and a bare anonymous object costs
  nothing to maintain where a dedicated contract type would be dead weight serving no other endpoint.
- **A limit in a reverse proxy instead.** Rejected for the same reason ADR 0018 rejected it: the facade
  should not depend on a proxy for its own protection, and the gateway (ADR 0033) is not itself a security
  boundary today.

## Related decisions

Resurrects ADR 0018's design against the endpoints ADR 0026 built to replace the password grant ADR 0035
removed. Leaves the same gaps ADR 0018 already named open (per-account limiting, account lockout, trusted
forwarded headers) and the multi-instance counter gap ADR 0028 already tracks.
