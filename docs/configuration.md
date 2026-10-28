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
`DB_*` is the MariaDB database, `PHPMYADMIN_*` is phpMyAdmin.

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
| `AUTHFACADE_HTTP_PORT` | `7128` | AuthFacade. Also forms the OAuth redirect URI `http://localhost:<port>/callback`. |

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
| `OPENEMR_ADMIN_USER` | `admin` | no | Admin login. Passed to the image as `OE_USER`. Also used by the live login tests. |
| `OPENEMR_ADMIN_PASSWORD` | none, required | yes | Admin password. Passed to the image as `OE_PASS`. A restored snapshot only accepts `ci-admin-password`. |

### AuthFacade runtime

| Variable | Default | Meaning |
| --- | --- | --- |
| `AUTHFACADE_ENVIRONMENT` | `Production` | ASP.NET Core environment of the AuthFacade container. `Development` adds the OpenAPI document and Scalar UI (`/scalar`), the developer exception page and debug logging, so use it only on your own machine. |

### AuthFacade OAuth client

| Variable | Default | Secret | Meaning |
| --- | --- | --- | --- |
| `AUTHFACADE_CLIENT_ID` | none, required | no | Client ID the seed service registers in OpenEMR. Becomes `OpenEmr__ClientId`. |
| `AUTHFACADE_CLIENT_SECRET` | none, required | yes | Client secret. Becomes `OpenEmr__ClientSecret`; `apply-credentials.sh` writes it to OpenEMR. |

### Integration tests

| Variable | Default | Meaning |
| --- | --- | --- |
| `STACK_READY_TIMEOUT_MINUTES` | `3` in code, `2` in the template | How long the readiness gate waits for the stack. Use 2 for a warm stack, 30 for a fresh install. |

The integration tests read every variable above from the process environment first, then `.env`,
then a built-in default. They also accept these optional overrides, which are not in `.env.example`
because the defaults match the stack:

| Variable | Default |
| --- | --- |
| `AUTHFACADE_TOKEN_PATH` | `/api/v1/connect/token` |
| `AUTHFACADE_REDIRECT_URI` | `http://localhost:<AUTHFACADE_HTTP_PORT>/callback` |
| `OPENEMR_DISCOVERY_PATH` | `/oauth2/default/.well-known/openid-configuration` |
| `OPENEMR_TOKEN_PATH` | `/oauth2/default/token` |

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
| `Scope` | `openid api:oemr` | Scope requested in the password grant. Compose sets `openid profile email offline_access`. |
| `AcceptAnyServerCertificate` | `false` | Trust any OpenEMR certificate. The host refuses to start with this set outside Development. |

### `OpenEmr:CircuitBreaker`

Guards calls to OpenEMR. Defaults open the circuit when half of at least five calls in 30 seconds fail,
and keep it open for 30 seconds.

| Key | Default | Rule |
| --- | --- | --- |
| `FailureRatio` | `0.5` | Above 0, at most 1. |
| `SamplingDuration` | `00:00:30` | At least 500 ms. |
| `MinimumThroughput` | `5` | At least 1. |
| `BreakDuration` | `00:00:30` | At least 500 ms. |

### `LoginRateLimit`

Limits login requests per client address.

| Key | Default | Rule |
| --- | --- | --- |
| `PermitLimit` | `10` | Most requests per window. At least 1. |
| `Window` | `00:01:00` | Fixed window length. At least one second. |

### Running outside Docker

`appsettings.Development.json` points `OpenEmr:BaseUrl` and `Issuer` at `https://localhost:9300`
and sets `AcceptAnyServerCertificate`. It cannot read `.env`, so edit it if you change
`OPENEMR_HTTPS_PORT`. `ClientId` and `ClientSecret` must be supplied, for example with
`dotnet user-secrets`.

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
