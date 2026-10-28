# 0020. The local stack is conservative by default: Production, loopback ports, non-root

- **Status:** Accepted
- **Date:** 2026-10-28
- **Amends:** the statement in [0015](0015-development-certificate-bypass-only-in-development.md) that the Compose stack runs as Development

## Context

The Compose stack was set up for convenience. The AuthFacade container always ran as `Development`, which turns on the OpenAPI document and Scalar UI, the developer exception page that shows stack traces, and debug logging for token handling. Every published port bound to all interfaces, so MariaDB and phpMyAdmin were reachable from the local network with credentials that are generated but sit in a `.env` file. The AuthFacade image ran its process as root. The stack's credentials, and the admin password of the restored snapshot, are known and shared with CI.

## Decision

1. **The AuthFacade container runs as `Production` by default.** The environment comes from `AUTHFACADE_ENVIRONMENT`. Setting it to `Development` turns the development features back on, for use on a developer's own machine.
2. **Every published port binds to `HOST_BIND_ADDRESS`, which defaults to `127.0.0.1`.** That covers MariaDB, phpMyAdmin, OpenEMR and AuthFacade. Setting `0.0.0.0` exposes the stack on purpose.
3. **The AuthFacade image runs as the non-root `app` user** that the .NET runtime image provides. The service listens on port 8080 and needs no privileges.
4. **The defaults are documented.** `.env.example` and `docs/configuration.md` describe both variables and the risk of changing them.

## Consequences

- A fresh stack exposes no database, no admin tool and no development endpoints beyond the machine it runs on.
- The Scalar UI and OpenAPI document are off unless `AUTHFACADE_ENVIRONMENT=Development`. `appsettings.Development.json` is not loaded in Production. The stack does not need it: Compose sets every `OpenEmr:*` value, and the certificate bypass does not apply to an `http://` base URL.
- CI builds its `.env` from `.env.example`, so it now runs the facade as Production.
- Remote access, such as a phone or another machine on the network, needs `HOST_BIND_ADDRESS=0.0.0.0`. Do it only on a trusted network, because the credentials are well known.
- The rate limit of [0018](0018-rate-limit-the-login-endpoint-per-client-address.md) is unchanged. It keys on the remote address, which is typically the Docker gateway address for requests through a published port.

## Not covered

- **TLS and proxy settings.** The service still listens on plain HTTP, sends no HSTS or security headers, and does not read forwarded headers. A deployment behind a proxy must add them.
- **A limit per account.** See [0018](0018-rate-limit-the-login-endpoint-per-client-address.md).
- **The `Development` opt-in inside the container** is not covered by a test.

## Alternatives considered

- **Keep `Development` and document the risk.** Rejected. A safe default costs one variable, and a documented risk does not stop a stack from being exposed.
- **Bind only the database and phpMyAdmin to loopback.** Rejected. The apps use the same default credentials, and one rule is easier to state and to override.
- **Drop to Production only in CI.** Rejected. The local stack is what people actually run, and CI should test what they run.
