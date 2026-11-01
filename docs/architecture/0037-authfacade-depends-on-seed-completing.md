# 0037. AuthFacade depends on the seed completing successfully

- **Status:** Accepted
- **Date:** 2026-11-01

## Context

The `seed` service (ADR 0005, ADR 0025) is a one-shot Compose service, `restart: "no"`, with no other
service naming it in a `depends_on`. Nothing made its completion a prerequisite for anything else; it ran
only when `docker compose up` happened to include it, either because the whole stack was brought up with no
service names, or because it was named explicitly.

This went unnoticed until a real login failed with `invalid_client` / "Client authentication failed" —
OpenEMR's generic error for `CustomAuthCodeGrant::validateRedirectUri()` rejecting a `redirect_uri` it does
not recognize. The `oauth_clients` row for `authfacade` still held `http://localhost:7128/callback`
(AuthFacade's own port) while `auth-facade` itself was sending `http://localhost:7130/callback` (the
gateway's port, ADR 0033). The seed had last run before the redirect URI's formula moved from
`AUTHFACADE_HTTP_PORT` to `GATEWAY_HTTP_PORT`, and a subsequent `docker compose up` that brought up
`auth-facade` (and the rest of the application services) without naming `seed` left the stale container in
place — Compose only recreates a one-shot service's container when something in that `up` invocation asks
for it.

The seed script itself was never the problem: ADR 0025 made it safe to rerun (it refreshes `scope`,
`redirect_uri`, `client_secret` and `is_confidential` unconditionally on every run). The gap was that nothing
*required* a rerun when the values it writes drifted from what `auth-facade` was actually configured with.

## Decision

1. **`auth-facade` gains a second `depends_on` entry: `seed: condition: service_completed_successfully`**,
   alongside the existing `openemr: condition: service_healthy` (ADR 0036). Compose pulls in a service's
   dependencies even when only the dependent service is named, so any `docker compose up` that starts or
   recreates `auth-facade` — by name or as part of the whole stack — now also evaluates `seed`.
2. **No change to the seed script itself.** `service_completed_successfully` only requires that a container
   satisfying the service's current config has exited 0 at some point; it does not force a rerun on its own.
   The actual fix is what this unlocks: Compose's own config-hash comparison already recreates a one-shot
   container when its resolved environment changes (`AUTHFACADE_CLIENT_ID`, `AUTHFACADE_CLIENT_SECRET`,
   `AUTHFACADE_REDIRECT_URI`, or the formula that builds any of them), but only for an `up` that actually
   considers that service. Making `seed` a real dependency of `auth-facade` is what makes every such `up`
   consider it.
3. **`gateway` and `login-ui` are not given the same dependency.** Neither calls OpenEMR's OAuth endpoints
   or holds `OpenEmr:*` settings; only `auth-facade` cares whether the registered client matches.

## Consequences

- A `docker compose up` (full stack, or naming `auth-facade` specifically) now always reseeds the client
  registration first when `AUTHFACADE_CLIENT_ID`, `AUTHFACADE_CLIENT_SECRET`, `AUTHFACADE_REDIRECT_URI`, or
  the Compose formula computing any of them, has changed since `seed` last ran — closing the gap this ADR
  was written for.
- `docker compose up` with no config changes still leaves the existing exited `seed` container alone; this
  does not make every `up` reseed unconditionally, only when Compose would have recreated `seed` anyway.
- A targeted `docker compose up mysql` or `docker compose up openemr` alone still will not run `seed` — it
  has no dependents naming it other than `auth-facade`. This is intentional: those commands have no reason
  to touch the OAuth client.
- Does not address the integration tests' own `AUTHFACADE_REDIRECT_URI` default drifting from the seed's
  actual value (`docs/configuration.md`, "Integration tests") — that default is read directly by the test
  process, outside Compose, and is unaffected by a `depends_on` change here.

## Alternatives considered

- **Give `seed` a content-based cache key (bake a hash of its own relevant env vars into a file or label,
  and compare it on every run) so the script itself detects drift and exits non-zero if stale.** Rejected:
  Compose already recreates the container on a resolved-config change; the actual gap was that nothing
  pulled `seed` into the dependency graph of the services that needed it current, not that the script
  couldn't tell it was stale.
- **Make AuthFacade verify its own `RedirectUri` against OpenEMR's registration at startup**, failing fast
  on a mismatch. Rejected: AuthFacade has no business reading `oauth_clients` directly — that is exactly the
  seed's job, and ADR 0001/0007 already keep AuthFacade out of OpenEMR's own schema.
- **Add `seed` as a dependency of `gateway` and `login-ui` too**, for symmetry. Rejected: neither reaches
  OpenEMR's OAuth endpoints; the extra edges would be dead weight, not defense-in-depth.

## Related decisions

Builds on ADR 0005 and ADR 0025's seed design, and ADR 0036's precedent of tightening `auth-facade`'s
`depends_on` to close a startup-ordering gap. Fixes the practical consequence of the redirect URI formula
ADR 0033 changed never reaching an already-provisioned client.
