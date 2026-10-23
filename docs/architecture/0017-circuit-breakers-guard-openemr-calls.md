# 0017. Circuit breakers guard calls to OpenEMR

- **Status:** Accepted
- **Date:** 2026-10-24

## Context

AuthFacade calls OpenEMR in two places: the password grant on `POST /connect/token`, and the JWKS fetch that the token verifier makes to check OpenEMR-issued tokens. When OpenEMR is down or slow, every login and every `/me` call waits for a timeout, which ties up the API and makes the outage worse. A circuit breaker stops the calls once OpenEMR is clearly failing, and lets a trial call through after a break.

## Decision

1. **Each breaker is a decorator over an application port.** `CircuitBreakingIdentityProvider` wraps `IIdentityProvider`, and `CircuitBreakingTokenVerifier` wraps `IExternalTokenVerifier`. Both live in Infrastructure. The application layer and the API see only the ports. A breaker at the HTTP message layer was rejected because it could not tell a refused login from an outage.
2. **Breaker state is shared through one singleton pipeline per breaker.** The wrapped identity provider is transient, as its HTTP client is. The token verifier is a singleton, since it caches keys.
3. **Only outage-like outcomes count as failures.**
   - Identity provider: an unreachable OpenEMR, a timeout, an unusable success response, and a 408 or 5xx status.
   - Token verifier: an unreachable JWKS endpoint.

   A refused login (a 4xx) does not count, because a wrong password is not an outage. A token that fails verification does not count either, because it says nothing about OpenEMR's health.
4. **An open circuit fails fast.** The call throws the port's unavailable exception without reaching OpenEMR. Login returns 503. `/me` refuses the token, as ADR 0013 describes for unavailable keys.
5. **No retries.** A retried password grant sends extra credential attempts to OpenEMR, which can trigger its lockouts.
6. **Thresholds are configuration.** They are bound from `OpenEmr:CircuitBreaker`, with defaults of a 50% failure ratio, at least 5 calls in 30 seconds, and a 30-second break. Startup refuses a failure ratio outside (0, 1], a minimum throughput below 1, or a sampling or break duration under 500 milliseconds. Polly rejects shorter durations.

## Consequences

- OpenEMR's outages stop reaching clients as slow requests. Clients get a fast 503 or 401 until a trial call succeeds.
- A bad OpenEMR deployment that returns 200 responses without a token opens the login breaker. That is deliberate, so it is visible as 503 rather than as repeated 502s.
- Each breaker has one shared state. A restart resets it, and it is not shared across API instances.
- The breakers are not in the HTTP client, so the HTTP client's own timeouts are still what cuts off a slow call. A timeout counts as a failure once it reaches the breaker.

## Alternatives considered

- **A breaker in the HTTP handler chain.** Rejected. It sees only HTTP messages, so it counts refusals as failures unless it parses bodies.
- **Retries with backoff.** Rejected for the password grant, for the reason in point 5. The JWKS fetch is cached and could retry, but it is not needed yet.
