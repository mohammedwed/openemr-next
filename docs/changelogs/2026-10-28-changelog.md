# Changelog 2026-10-28

## Summary

Cleaned up the repository, made `.env` the single source for configuration, reorganized and documented the scripts, and reviewed the token endpoint. The endpoint now binds typed form parameters, and the stack defaults are safer. The decisions are recorded in ADRs 0019 and 0020. The work landed in pull requests 22 and 23, and pull request 24 carries the endpoint and security changes.

## Breaking changes

- **Renamed `.env` variables.** `WT_HTTP_PORT`, `WT_HTTPS_PORT`, `WT_MYSQL_PORT`, `WT_PMA_PORT`, `FACADE_HTTP_PORT`, `OE_USER` and `OE_PASS` are now `OPENEMR_HTTP_PORT`, `OPENEMR_HTTPS_PORT`, `DB_PORT`, `PHPMYADMIN_PORT`, `AUTHFACADE_HTTP_PORT`, `OPENEMR_ADMIN_USER` and `OPENEMR_ADMIN_PASSWORD`. Old names are ignored. `up.sh` warns about them, and `docs/configuration.md` has a migration command. The GitHub secret is still `OE_PASS`.
- **Health check moved** from `/api/health` to `/health`. `/api/health` returns 404. Update any external probe.
- **`unsupported_grant_type` is no longer returned.** A missing or unsupported grant type is 400 `invalid_request`. See ADR 0019.
- **A non-form request to `POST /api/v1/connect/token` returns 415 with no body.** It returned 400 with a body.
- **Script paths changed.** See Changed.

## Added

- **Typed form binding on the token endpoint.** See ADR 0019.
  - `PasswordGrant` accepts only the `password` grant and delegates to `GrantType`.
  - `AddOAuthErrorBodies` and `UseOAuthErrorBodies` give an OAuth body to the framework's empty 400. The middleware throws at startup if the service setting is missing.
  - `AsPublicTokenEndpoint` groups the anonymous, no-antiforgery, error-body and rate-limit conventions.
  - `ErrorResponse.InvalidRequest()`, `InvalidGrant()`, `TooManyRequests()` and `ServerError()` spell each OAuth code once. `TokenResponse.From` builds the response from an `OpenEmrToken`.
  - The endpoints are named `IssueToken` and `GetCurrentUser`, which become their OpenAPI `operationId`.
- **Safer stack defaults.** See ADR 0020.
  - `AUTHFACADE_ENVIRONMENT` sets the facade's environment and defaults to `Production`.
  - `HOST_BIND_ADDRESS` sets the address of every published port and defaults to `127.0.0.1`.
  - The AuthFacade image runs as the non-root `app` user.
- **`docs/configuration.md`**, a reference for every `.env` variable and AuthFacade setting. `.env.example` comments every group.
- **`scripts/README.md`**, with requirements, optional variables and a snapshots section.
- **`scripts/lib/env.sh`**, shared shell helpers (`env_get`, `env_set`, `sql_quote`, `env_warn_legacy`) used by the scripts and CI.
- **`scripts/snapshot/create.sh`**, the bash snapshot script.

- **A fuller OpenAPI document.** Only in Development, as before.
  - The title, description, summaries and the `Authentication` and `Identity` tags describe the API.
  - A `Bearer` security scheme is declared, and `GET /api/v1/me` is marked as secured, so the Scalar UI offers an Authorize field.
  - Every response is documented with its body: 200, 400, 415, 429, 502 and 503 for the token endpoint, and 200 and 401 for `/me`. `TokenResponse`, `ErrorResponse` and `MeResponse` are now schemas.
  - The token operation lists only `application/x-www-form-urlencoded`. It listed `multipart/form-data` first, which is what Scalar sent.

## Changed

- **`.env` is the single source.** Ports, the database user and name, and the OAuth redirect URI come from `.env` in the scripts, seed SQL, tests and CI. Before, they were hardcoded as 9300, 7128 and `openemr`.
- **CI builds `.env` from `.env.example`**, so image pins no longer drift from the template.
- **`StackConfig` strips quotes from `.env` values**, as the shell parser does.
- **`auth-facade` depends on `openemr`**, not `mysql`. It has not used the database since its connection string was removed.
- **Scripts are grouped by purpose:** `scripts/{setup,stack,snapshot,seeds,lib}`. `env-management/` and `db-seeds/` are gone, `apply-env.sh` is now `stack/apply-credentials.sh`, and the seed files use one spelling, `authfacade-client`. Script headers document usage, requirements, inputs and side effects.
- **`up.sh --restore` is the only restore path.** It wipes the volumes, restores the snapshot and re-applies credentials.
- **README** matches the repository: the structure tree, the commands and the prerequisites. `docs/changelogs/story.txt` became `2026-10-11-story-facade-before-delegated-login.md`.
- **The `LoginRateLimitOptions.PermitLimit` comment** says "most", not "fewest".

## Removed

- The tracked `.vs/` directory, which `.gitignore` already covered.
- `scripts/db-migration/`: the 15 MB `baseline.sql`, a second compose file and an old Dockerfile. The stack restores from the snapshot release.
- `scripts/restore.ps1` and `scripts/snapshot/create.ps1`. The PowerShell snapshot script wrote its dump inside the container, which fails on a stack restored by `up.sh`.
- `ConnectionStrings__DefaultConnection` from the AuthFacade service, and `StackConfig.AuthFacadeLoginPath`. Nothing used them.

## Fixed

- **The OpenAPI document described the token endpoint's form fields as objects.** The generator reflected the value-object types, so `username` was an object with a `value` property and `password` had no properties. The Scalar UI sent those fields in a form the endpoint did not read as plain text, and logins failed with `invalid_grant`. A schema transformer now describes `PasswordGrant`, `Username` and `Password` as strings with their length limits, and `Password` as `format: password`.
- **Snapshot creation on a restored stack.** `create.sh` streams the dump to the host, so a root-owned `/tmp/db.sql` left by the restore no longer blocks it. It hands the archive back to the invoking user and restarts the apps even when a step fails.
- **MariaDB and phpMyAdmin were reachable from the local network.** They now bind to `127.0.0.1` by default.

## Tests

- Unit tests: 141 pass, up from 127. New tests cover `PasswordGrant`, the pairing of the error-body setting and middleware, the endpoint names, the new error cases, and `/api/health` returning 404.
- Architecture tests: 12 pass. Integration tests: 13 pass against the rebuilt stack.
- `up.sh --restore` ran end to end in an isolated copy of the repository. The seed script ran against a real `oauth_clients` schema. The pull request 22 CI run on `main` passed.
- Not covered: the CI run for pull request 24, the `Development` opt-in in the container, and `apply-credentials.sh` on its own.
