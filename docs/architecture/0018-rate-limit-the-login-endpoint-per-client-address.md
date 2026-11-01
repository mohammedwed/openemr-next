# 0018. The login endpoint is rate limited per client address

- **Status:** Superseded by [0035](0035-password-grant-and-bearer-auth-are-removed.md)
- **Date:** 2026-10-24

## Context

`POST /connect/token` takes a username and password and forwards them to OpenEMR. Nothing stops a client from sending guesses as fast as the network allows. Each guess also reaches OpenEMR, so a flood of guesses loads OpenEMR as well as AuthFacade. OpenEMR has its own lockouts, but they are per account, and they do not protect the facade's own resources.

## Decision

1. **The limit uses ASP.NET Core's built-in rate limiter.** It ships in the .NET 10 shared framework, so it adds no package. The API host registers it in `AddLoginRateLimiting`, and `UseAuthFacadeHttpMiddleware` runs it before authentication.
2. **The token endpoint has a fixed-window limit per client address.** Every request counts, whether it succeeds or fails. The window is `LoginRateLimit:PermitLimit` requests per `LoginRateLimit:Window`, with defaults of 10 requests per minute. The limiter does not queue: an over-limit request is refused at once.
3. **An over-limit request gets 429 `too_many_requests`.** The response carries a `Retry-After` header in whole seconds, and it uses the same `ErrorResponse` shape as the other errors. OpenEMR is not called.
4. **Only the token endpoint is limited.** `/me` checks a token locally, and it does not guess passwords, so it stays unlimited.
5. **Thresholds are configuration, and the host validates them at startup.** They are bound from `LoginRateLimit` in the API host. Startup refuses a permit limit below 1 or a window under one second.
6. **The limiter is in the API host, not in Infrastructure.** It depends on the ASP.NET Core pipeline, which the layering rules in ADR 0007 keep out of the layers below the API.

## Consequences

- A client that sends too many requests in a minute is refused without reaching OpenEMR. Brute force from one address is slowed to the configured rate.
- A legitimate user behind a shared address, such as an office NAT, can hit the limit when many people log in together. The limit is per address, so it cannot tell them apart.
- The limiter keys on `RemoteIpAddress`. Behind a reverse proxy, every request looks like it came from the proxy, so the proxy has to be trusted for forwarded headers before the limit means anything. Until that is configured, a deployment behind a proxy shares one limit across all clients.
- The limiter's counters are in memory. A restart resets them, and each API instance counts on its own. A multi-instance deployment therefore allows up to N times the limit.

## Not covered

- **A limit per account.** A distributed attack that spreads guesses across many addresses, against one account, is not stopped by a per-address limit. A per-username limit would need the form read before the check, so it is left for a later decision.
- **Lockout of an account.** OpenEMR owns account lockout, and this decision does not change it.

## Alternatives considered

- **A limit in the reverse proxy.** Rejected for now. The facade should not depend on a proxy for its own protection, and this repo has no proxy configured yet.
- **A third-party rate limiting package.** Rejected. The built-in limiter covers this need without a dependency.
- **Counting only failed logins.** Rejected. Counting failures would need the outcome before the check, and the limiter runs before the handler, so every request counts.
