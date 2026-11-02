# Changelog 2026-11-02: OpenIddict's certificates are persisted files

## Summary

The last of ADR 0028's three gaps: OpenIddict's encryption and signing certificates were still dev-only
and regenerated fresh on every process start (and, inside a container, every restart is effectively a
fresh process). ADR 0043/0044 already made the state token this certificate protects persist in a
database; persisting the row while the key that unlocks it disappeared on every restart made that only
partly real. Closed by a new, persisted pair of certificates — the same file-based, password-less pattern
ADR 0039 already established for the gateway's own TLS certificate. See ADR 0046.

## Added

- **`scripts/setup/new-authfacade-certs.sh`** generates two self-signed PKCS#12 (`.pfx`) certificates —
  encryption and signing — into `certs/` (git-ignored, same directory as the gateway's own certificate).
  No password: these protect nothing but transport/at-rest confidentiality on this machine, the same
  choice `new-dev-cert.sh` already made. Confirmed live that .NET's PKCS#12 loader accepts an empty
  password from OpenSSL 3's default (non-legacy) export — no `-legacy` flag needed.
- **`AuthFacadeCertificateOptions`**, bound from a new `AuthFacadeCertificates` section — the same
  optional-as-a-whole shape as `AuthFacadeDbOptions` (ADR 0043). When unconfigured, AuthFacade falls back
  to OpenIddict's own development certificates, unchanged from before this ADR — every unit test and a
  bare `dotnet run` still take this path.
- `docker-compose.yml`: `auth-facade` now mounts `./certs:/certs:ro` (the same mount the gateway already
  has) and points `AuthFacadeCertificates__*` at the two generated files unconditionally. **This makes
  `scripts/setup/new-authfacade-certs.sh` a required step**, not an optional one: the stack fails to start
  `auth-facade` without it, the same way `new-env.sh` is already required for its secrets.

## Fixed

- CI starts `auth-facade` directly (`docker compose up -d --build --wait ... auth-facade ...`), never
  through the gateway, so it was about to hit the same "required file/variable missing" failure the
  `AUTHFACADE_DB_PASSWORD` miss caused earlier this week — caught and fixed *before* pushing this time: a
  new `ci.yml` step runs `new-authfacade-certs.sh` right before the stack starts.

## Tests

- Unit tests: 81 pass (up from 80). New: a test that generates two throwaway self-signed PFX files
  in-process (`CertificateRequest.CreateSelfSigned`, no shell-out needed) and drives a full login round
  trip pointed at them, proving the configured-certificate branch is genuinely exercised, not just that
  the app starts with it — the same standard `AuthFacadeDbTests` (ADR 0043) already set for the database
  branch.
- Verified live against the real stack, the actual point of this change: drove `/oidc/login` to create a
  state token (encrypted with the persisted certificate), restarted the `auth-facade` container, then
  drove `/callback` with that same pre-restart `state` value. The logs show OpenIddict successfully
  decrypting the state token's payload and extracting its PKCE `code_verifier` — only possible if the
  encryption certificate survived the restart — and proceeding all the way to a real token request to
  OpenEMR (which then rejected the accompanying fake authorization code, an expected, unrelated failure).
