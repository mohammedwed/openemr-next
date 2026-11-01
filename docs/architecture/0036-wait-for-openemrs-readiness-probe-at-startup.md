# 0036. AuthFacade waits for OpenEMR's readiness probe at startup

- **Status:** Accepted
- **Date:** 2026-11-01

## Context

`docker-compose.yml`'s `auth-facade` service depended on `openemr: condition: service_started` — the
weakest Compose dependency condition, satisfied the moment OpenEMR's container process launches, not when
OpenEMR itself is actually ready to serve requests. OpenEMR has no `healthcheck:` of its own, so there was
nothing stronger to depend on even if `auth-facade` asked for it. `mysql` does have one, and `openemr`
already depends on `mysql: condition: service_healthy`, so AuthFacade transitively waited for the database
— just not for OpenEMR, the thing it actually calls on every browser login and every proxied API request.

In practice this is a narrow window on a warm restart, but a real one on a fresh install or a slow machine:
OpenEMR's own startup routine (site setup, migrations) can take noticeably longer than its container taking
its first breath. A request that lands on AuthFacade during that window fails with a raw connection
exception from deep inside the OIDC or BFF-proxy call, not a clear, expected error.

## Decision

1. **`openemr` gets a real `healthcheck:`**, polling its own `/meta/health/readyz` (the same probe
   `ReadinessFixture` already polls for the integration tests, ADR 0006) with `curl`, confirmed available
   in the image. A 3-minute `start_period` and 10 retries at a 10-second interval give a slow first boot
   room without ever reporting falsely "unhealthy" — failures during `start_period` do not count toward
   `retries`, so this only costs time, never a wrongly-refused start.
2. **`auth-facade`'s `depends_on` tightens to `openemr: condition: service_healthy`.** Compose will not
   start the `auth-facade` container until that check passes, closing the window in the common (Compose)
   case entirely: by the time AuthFacade's own process starts, OpenEMR already answered its own readiness
   probe successfully.
3. **AuthFacade's own startup additionally polls the same probe** (`OpenEmrReadinessGate`,
   `OpenEmrNext.AuthFacade.Api/Startup/`), before `UseAuthFacadeHttpMiddleware()` runs. This is a second,
   independent line of defense for the cases Compose's own gate cannot cover: a bare `dotnet run` outside
   Docker, or ordinary timing slack between a healthcheck passing and Compose actually starting the next
   container. It polls every 2 seconds for up to 60 seconds, then — if OpenEMR still has not answered —
   logs a warning and lets the app start anyway: a request that then fails fails no worse than it would
   have without this check, and a host that never starts is worse than one that started slow.
4. **Not wired into `TestApi`.** Most unit tests point `OpenEmr:BaseUrl` at placeholder hosts
   (`https://openemr-internal.test` and similar) that are never meant to be reachable; waiting out a real
   timeout against them on every test run would make the suite slow for no benefit the tests need.
   `OpenEmrReadinessGate`'s polling logic is still unit-tested directly, against a fake `HttpMessageHandler`
   with short thresholds, independent of the real host.

## Consequences

- In the Compose stack, the startup-ordering gap is closed: AuthFacade's container does not even start
  until OpenEMR's own readiness probe has passed.
- Outside Compose, the gap narrows to a bounded 60-second grace window instead of being unbounded; it is
  not eliminated, since there is no way to guarantee OpenEMR's eventual readiness without Compose's own
  dependency ordering, and a never-booting OpenEMR must not hang AuthFacade's startup forever.
- Over plain HTTP (the Compose network's `OpenEmr:BaseUrl`), this works as described. Over the
  self-signed-certificate HTTPS `appsettings.Development.json` points at for non-Docker local dev
  (`https://localhost:9300`), the readiness poll will not succeed at all and will always time out after 60
  seconds before logging a warning and continuing — a slower, but still non-fatal, start. Resurrecting a
  TLS bypass to fix this is explicitly rejected: ADR 0035 removed `AcceptAnyServerCertificate` because
  nothing read it anymore, and this is not a strong enough reason to bring that complexity back for a path
  the project already treats as less-supported (see `docs/configuration.md`'s "Running outside Docker").

## Alternatives considered

- **A circuit breaker or per-request retry around OpenEMR calls**, instead of a one-time startup gate.
  Rejected: ADR 0035 deliberately removed the project's circuit breakers because nothing used them; adding
  ongoing per-request resilience back now, for a narrower problem (a one-time ordering race, not an
  ongoing failure mode), would partly undo that decision's own reasoning. A ready-or-not-yet gate at
  startup is the right size for what this actually is: an ordering problem, not a resilience problem.
- **Only the Compose healthcheck, no in-app gate.** Rejected: it solves nothing for a bare `dotnet run`,
  which the project explicitly still supports as a documented (if less-covered) path.
- **Only the in-app gate, no Compose healthcheck.** Rejected: Compose's own dependency ordering is the
  stronger, zero-cost guarantee for the common case; relying on a 60-second in-app poll alone would make
  every fresh-install Compose run slower than necessary for no reason, since Compose can already express
  "wait for this" natively.

## Related decisions

Builds on ADR 0006 (black-box integration tests), whose `ReadinessFixture` already established
`/meta/health/readyz` as OpenEMR's own readiness probe. Deliberately does not reintroduce ADR 0017's
circuit breakers, removed by ADR 0035, or ADR 0015's TLS bypass, also removed by ADR 0035.
