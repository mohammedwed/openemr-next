# Configuration reference

Configuration has three layers. Each setting is defined in one place and read from there.

| Layer | File | Holds |
| --- | --- | --- |
| Stack | `.env` (from `.env.example`, gitignored) | Images, host ports, credentials, test timeout |
| Container wiring | `docker-compose.yml` | Maps `.env` values onto each service's own settings |
| Application | `appsettings*.json`, or `Section__Key` environment variables | AuthFacade behaviour |

`.env` is the single source for anything shared between services, scripts, CI and tests (ADR 0004).
Run `bash scripts/setup/new-env.sh` to create it with generated secrets. `up.sh`
warns if `.env` still uses a renamed variable (see [Renamed variables](#renamed-variables)).

## Stack variables (`.env`)

Naming: `OPENEMR_*` is the OpenEMR container, `AUTHFACADE_*` is the AuthFacade service,
`LOGINUI_*` is the Login UI service, `DB_*` is the MariaDB database, `PHPMYADMIN_*` is phpMyAdmin.

### Images

| Variable | Default | Meaning |
| --- | --- | --- |
| `MARIADB_IMAGE` | none, required | MariaDB image, pinned by digest. Also used by the seed service. |
| `OPENEMR_IMAGE` | none, required | OpenEMR image. The snapshot matches `8.0.0.3`; changing it needs a new snapshot. |
| `PHPMYADMIN_IMAGE` | none, required | phpMyAdmin image, pinned by digest. |

### Host ports

| Variable | Default | Meaning |
| --- | --- | --- |
| `HOST_BIND_ADDRESS` | `127.0.0.1` | Address every published port binds to. The default keeps the database, phpMyAdmin and the apps off your network. Set `0.0.0.0` only to expose the stack on purpose; the default credentials are well known. |
| `OPENEMR_HTTP_PORT` | `8300` | OpenEMR over HTTP. |
| `OPENEMR_HTTPS_PORT` | `9300` | OpenEMR over HTTPS. Also forms the token issuer URL `https://localhost:<port>/oauth2/default`. |
| `DB_PORT` | `8320` | MariaDB. |
| `PHPMYADMIN_PORT` | `8310` | phpMyAdmin. |
| `AUTHFACADE_HTTP_PORT` | `7128` | AuthFacade, reached directly over plain HTTP (Scalar/OpenAPI, or direct API testing). Not what the Login UI's link uses (ADR 0039). |
| `LOGINUI_HTTP_PORT` | `7129` | Login UI. |
| `GATEWAY_HTTP_PORT` | `7130` | The gateway (ADR 0033), over plain HTTP: redirects to `GATEWAY_HTTPS_PORT` (ADR 0039) and otherwise serves nothing. |
| `GATEWAY_HTTPS_PORT` | `7131` | The gateway's HTTPS listener (ADR 0039): the browser's real entry point for both the WebApp dashboard and the Login UI's link. Run `scripts/setup/new-dev-cert.sh` first. Also forms the OAuth redirect URI `https://localhost:<port>/callback` (ADR 0005, ADR 0033, ADR 0039) — not `GATEWAY_HTTP_PORT`, which now only redirects to this port and serves nothing else. |

### Database

| Variable | Default | Secret | Meaning |
| --- | --- | --- | --- |
| `DB_NAME` | `openemr` | no | Database name. |
| `DB_USER` | `openemr` | no | Application user. Created on restore. |
| `DB_ROOT_PASSWORD` | none, required | yes | MariaDB root password. |
| `DB_PASSWORD` | none, required | yes | Password for `DB_USER`. `apply-credentials.sh` writes it to OpenEMR's `sqlconf.php`. |

### OpenEMR admin account

| Variable | Default | Secret | Meaning |
| --- | --- | --- | --- |
| `OPENEMR_ADMIN_USER` | `admin` | no | Admin login. Passed to the image as `OE_USER`. |
| `OPENEMR_ADMIN_PASSWORD` | none, required | yes | Admin password. Passed to the image as `OE_PASS`. A restored snapshot only accepts `ci-admin-password`. |

### AuthFacade runtime

| Variable | Default | Meaning |
| --- | --- | --- |
| `AUTHFACADE_ENVIRONMENT` | `Production` | ASP.NET Core environment of the AuthFacade container. `Development` adds the OpenAPI document and Scalar UI (`/scalar/v1`), the developer exception page and debug logging, so use it only on your own machine. |

### AuthFacade OAuth client

| Variable | Default | Secret | Meaning |
| --- | --- | --- | --- |
| `AUTHFACADE_CLIENT_ID` | none, required | no | Client ID the seed service registers in OpenEMR. Becomes `OpenEmr__ClientId`. |
| `AUTHFACADE_CLIENT_SECRET` | none, required | yes | Client secret. Becomes `OpenEmr__ClientSecret`; `apply-credentials.sh` writes it to OpenEMR. |

Not a `.env` variable, same as `AUTHFACADE_REDIRECT_URI`: `docker-compose.yml` also passes the seed service
`AUTHFACADE_POST_LOGOUT_REDIRECT_URI`, computed from `LOGINUI_HTTP_PORT`, registered as this client's
`logout_redirect_uris` (ADR 0040). Must exactly match what `/logout` sends as `post_logout_redirect_uri` —
OpenEMR compares it with a plain SQL `=`, not a host allow-list.

### AuthFacade database

| Variable | Default | Secret | Meaning |
| --- | --- | --- | --- |
| `AUTHFACADE_DB_NAME` | `authfacade` | no | AuthFacade's own dedicated database name, separate from OpenEMR's own `DB_NAME` (ADR 0043). Created by the `seed-authfacade-db` service. Becomes `AuthFacadeDb__Database`. |
| `AUTHFACADE_DB_USER` | `authfacade` | no | Dedicated, least-privilege user that can reach only that database. Becomes `AuthFacadeDb__User`. |
| `AUTHFACADE_DB_PASSWORD` | none, required | yes | Password for `AUTHFACADE_DB_USER`. Becomes `AuthFacadeDb__Password`; `seed-authfacade-db` writes it to the database. |

This holds only AuthFacade's own session-related state — OpenIddict.Client's state-token store and the
Data Protection key ring behind the `AuthFacadeSession` cookie — never OpenEMR's own data, on the same
`mysql` service. Outside Compose (tests, a bare `dotnet run`), no `AuthFacadeDb` section is configured and
AuthFacade falls back to a throwaway per-process SQLite file instead.

### Login UI runtime

| Variable | Default | Meaning |
| --- | --- | --- |
| `LOGINUI_ENVIRONMENT` | `Production` | ASP.NET Core environment of the Login UI container. `Development` adds the developer exception page and debug logging. |

The Login UI's own `AuthFacade__BaseUrl` is not a stack variable: `docker-compose.yml` derives it from
`GATEWAY_HTTPS_PORT` (ADR 0039), since the browser follows the login link to the gateway's HTTPS address,
not AuthFacade's own plain-HTTP port or the Compose-internal `auth-facade` hostname.

### Gateway runtime

| Variable | Default | Meaning |
| --- | --- | --- |
| `GATEWAY_ENVIRONMENT` | `Production` | ASP.NET Core environment of the gateway container. |

The gateway's own `AuthFacade__BaseUrl` and `WebApp__BaseUrl` are not stack variables either:
`docker-compose.yml` sets them to the Compose-internal `auth-facade`/`webapp` hostnames directly, since the
gateway reaches both over the Compose network, never through a host-published port.

### Integration tests

| Variable | Default | Meaning |
| --- | --- | --- |
| `STACK_READY_TIMEOUT_MINUTES` | `3` in code, `2` in the template | How long the readiness gate waits for the stack. Use 2 for a warm stack, 30 for a fresh install. |

The integration tests read every variable above from the process environment first, then `.env`,
then a built-in default. They also accept these optional overrides, which are not in `.env.example`
because the defaults match the stack:

| Variable | Default |
| --- | --- |
| `AUTHFACADE_REDIRECT_URI` | `http://localhost:<GATEWAY_HTTP_PORT>/callback` |
| `OPENEMR_DISCOVERY_PATH` | `/oauth2/default/.well-known/openid-configuration` |
| `OPENEMR_TOKEN_PATH` | `/oauth2/default/token` |
| `OPENEMR_AUTHORIZE_PATH` | `/oauth2/default/authorize` |

`AuthFacadeStackTests.OpenEmr_accepts_the_AuthFacade_redirect_uri` hits the authorize endpoint directly with
this value, so a redirect_uri that drifts from what the seed actually registers (the gateway's address, ADR
0033) now fails the integration suite instead of only surfacing in a real browser login (ADR 0037).

## AuthFacade settings

Set in `appsettings.json` or as environment variables with `__` for `:` (for example
`OpenEmr__ClientId`). Compose sets the `OpenEmr__*` values for the container.

### `OpenEmr`

| Key | Default | Meaning |
| --- | --- | --- |
| `BaseUrl` | required | URL the service uses to reach OpenEMR, and its JWKS. In Compose: `http://openemr:80`. |
| `Issuer` | required | Issuer OpenEMR writes into tokens. Can differ from `BaseUrl` when reached over another network. |
| `ClientId` | required | OAuth client ID. From `AUTHFACADE_CLIENT_ID`. |
| `ClientSecret` | required | OAuth client secret. From `AUTHFACADE_CLIENT_SECRET`. |
| `RedirectUri` | required | Redirect URI registered for this client, used by the browser flow's `/login` and `/callback` (ADR 0024). Must match the seed's `redirect_uri` exactly. In Compose, the gateway's address, not AuthFacade's own (ADR 0033). `auth-facade` depends on `seed` completing (ADR 0037), so `docker compose up` reseeds automatically when this drifts. |

### `AuthFacadeDb`

AuthFacade's own dedicated database (ADR 0043): OpenIddict.Client's state-token store and the Data
Protection key ring behind the `AuthFacadeSession` cookie. Deliberately optional as a whole — when `Host`
is unset, AuthFacade falls back to a throwaway per-process SQLite file instead, which every unit test and
a bare `dotnet run` outside Docker rely on. Only the real Compose stack sets this section.

| Key | Default | Meaning |
| --- | --- | --- |
| `Host` | `""` (SQLite fallback) | MariaDB server hostname. In Compose: `mysql`. |
| `Port` | `3306` | MariaDB server port. |
| `Database` | `""` | Dedicated database name, separate from OpenEMR's own. From `AUTHFACADE_DB_NAME`. |
| `User` | `""` | Dedicated, least-privilege user that can reach only `Database`. From `AUTHFACADE_DB_USER`. |
| `Password` | `""` | That user's password. From `AUTHFACADE_DB_PASSWORD`. |

### `OpenEmr:CircuitBreaker`

Guards the token exchange, the JWKS fetch and the BFF proxy's forwarded calls (ADR 0041) — three
independent breakers, one set of thresholds. A 4xx from OpenEMR never counts as a failure; only a
connection problem, a timeout, a `408`, or a `5xx` does.

| Key | Default | Rule |
| --- | --- | --- |
| `FailureRatio` | `0.5` | Above 0, at most 1. |
| `SamplingDuration` | `00:00:30` | At least 500 ms. |
| `MinimumThroughput` | `5` | At least 1. |
| `BreakDuration` | `00:00:30` | At least 500 ms. |

### `BrowserFlow`

Governs where the browser authorization-code flow (ADR 0024) sends the browser.

| Key | Default | Meaning |
| --- | --- | --- |
| `DefaultReturnUrl` | required | Where `/login` and `/callback` send the browser with no usable `returnUrl`. Points at the Login UI. Also where `/logout` always sends the browser (ADR 0040) — logout has no caller-supplied `returnUrl` of its own, and this is the one value registered as `AUTHFACADE_POST_LOGOUT_REDIRECT_URI`. |
| `ReturnUrlAllowedHosts` | required, at least one | Hosts a caller-supplied `returnUrl` may resolve to; anything else falls back to `DefaultReturnUrl`. Not read by `/logout`. |

### `LoginRateLimit`

Limits requests to `/oidc/login` and `/callback` per client address (ADR 0038). Every request counts,
successful or not; an over-limit request gets 429 with a `Retry-After` header and is never forwarded to
OpenEMR.

| Key | Default | Rule |
| --- | --- | --- |
| `PermitLimit` | `10` | At least 1. |
| `Window` | `00:01:00` | At least one second. |

### Running outside Docker

`appsettings.Development.json` points `OpenEmr:BaseUrl` and `Issuer` at `https://localhost:9300`.
It cannot read `.env`, so edit it if you change `OPENEMR_HTTPS_PORT`. `ClientId` and `ClientSecret`
must be supplied, for example with `dotnet user-secrets`.

## Renamed variables

Older `.env` files may use these names. They are now ignored, and compose falls back to defaults.

| Old | New |
| --- | --- |
| `WT_HTTP_PORT` | `OPENEMR_HTTP_PORT` |
| `WT_HTTPS_PORT` | `OPENEMR_HTTPS_PORT` |
| `WT_MYSQL_PORT` | `DB_PORT` |
| `WT_PMA_PORT` | `PHPMYADMIN_PORT` |
| `FACADE_HTTP_PORT` | `AUTHFACADE_HTTP_PORT` |
| `OE_USER` | `OPENEMR_ADMIN_USER` |
| `OE_PASS` | `OPENEMR_ADMIN_PASSWORD` |

To migrate an existing `.env`:

```bash
sed -i -e 's/^WT_HTTP_PORT=/OPENEMR_HTTP_PORT=/' -e 's/^WT_HTTPS_PORT=/OPENEMR_HTTPS_PORT=/' \
  -e 's/^WT_MYSQL_PORT=/DB_PORT=/' -e 's/^WT_PMA_PORT=/PHPMYADMIN_PORT=/' \
  -e 's/^FACADE_HTTP_PORT=/AUTHFACADE_HTTP_PORT=/' \
  -e 's/^OE_USER=/OPENEMR_ADMIN_USER=/' -e 's/^OE_PASS=/OPENEMR_ADMIN_PASSWORD=/' .env
```

The GitHub Actions secret is still named `OE_PASS`; only the `.env` name changed.
