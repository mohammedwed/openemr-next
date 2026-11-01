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
| `AUTHFACADE_HTTP_PORT` | `7128` | AuthFacade, reached directly (by the Login UI's link, or for direct API testing). |
| `LOGINUI_HTTP_PORT` | `7129` | Login UI. |
| `GATEWAY_HTTP_PORT` | `7130` | The gateway (ADR 0033): the browser's entry point for the WebApp dashboard. Also forms the OAuth redirect URI `http://localhost:<port>/callback` — not `AUTHFACADE_HTTP_PORT` — since the dashboard's session cookie must be scoped to whichever origin OpenEMR redirects back to. |

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

### Login UI runtime

| Variable | Default | Meaning |
| --- | --- | --- |
| `LOGINUI_ENVIRONMENT` | `Production` | ASP.NET Core environment of the Login UI container. `Development` adds the developer exception page and debug logging. |

The Login UI's own `AuthFacade__BaseUrl` is not a stack variable: `docker-compose.yml` derives it from
`AUTHFACADE_HTTP_PORT` directly, since the browser follows the login link to AuthFacade's host-published
address, not the Compose-internal `auth-facade` hostname.

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

### `BrowserFlow`

Governs where the browser authorization-code flow (ADR 0024) sends the browser.

| Key | Default | Meaning |
| --- | --- | --- |
| `DefaultReturnUrl` | required | Where `/login` and `/callback` send the browser with no usable `returnUrl`. Points at the Login UI. |
| `ReturnUrlAllowedHosts` | required, at least one | Hosts a caller-supplied `returnUrl` may resolve to; anything else falls back to `DefaultReturnUrl`. |

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
