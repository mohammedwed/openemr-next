# 0046. OpenIddict's encryption and signing certificates are persisted files

- **Status:** Accepted
- **Date:** 2026-11-02
- **Builds on:** [0028](0028-browser-login-and-session-assume-a-single-instance.md), [0039](0039-gateway-terminates-tls-authfacade-trusts-forwarded-headers.md), [0043](0043-authfacade-session-state-in-a-dedicated-mariadb-database.md)

## Context

ADR 0028 named three independent gaps blocking more than one AuthFacade instance, all rooted in nothing
being shared across processes. ADR 0043/0044 closed two of them — OpenIddict's state-token store and the
Data Protection key ring behind the `AuthFacadeSession` cookie now persist in a dedicated MariaDB database,
surviving a restart. The third was explicitly left open: `AddDevelopmentEncryptionCertificate()` and
`AddDevelopmentSigningCertificate()` generate (or reuse, from the OS certificate store) a fresh
self-signed certificate every process start. Inside a container, that store itself lives on an ephemeral
filesystem, so every restart is effectively a fresh certificate regardless.

This mattered for more than multi-instance deployment. The state token ADR 0043's own database persists
is *encrypted with this certificate* — persisting the row while the key that unlocks it disappears on
every restart made that persistence partly theatrical: a login in flight across a restart (the ~5-minute
window ADR 0028 originally scoped this risk to) still failed, decryption error instead of a missing row.

## Decision

1. **Two self-signed certificates, generated once**, by a new `scripts/setup/new-authfacade-certs.sh` —
   the exact same shape `scripts/setup/new-dev-cert.sh` already established for the gateway's own TLS
   certificate (ADR 0039): written to `certs/` at the repo root (git-ignored, "regenerate by deleting"),
   never overwritten if they already exist. Packaged as password-less PKCS#12 (`.pfx`) files, the format
   OpenIddict.Client's own `AddEncryptionCertificate`/`AddSigningCertificate` stream overloads accept.
2. **`AuthFacadeCertificateOptions`**, bound from a new `AuthFacadeCertificates` section — the same
   optional-as-a-whole shape as `AuthFacadeDbOptions` (ADR 0043): when `EncryptionCertificatePath` is
   unset, AuthFacade falls back to OpenIddict's own development certificates, the behavior every unit test
   and a bare `dotnet run` still rely on.
3. **No password on the PFX files** — the same "no passphrase" choice `new-dev-cert.sh` already made for
   the gateway's own certificate: these protect nothing but transport/at-rest confidentiality on this
   machine (ADR 0020's local-dev maturity level), so a password would be a secret to manage for no real
   gain. Confirmed live that .NET's PKCS#12 loader accepts an empty password from OpenSSL 3's default
   (non-legacy) export.
4. **Mounted into the `auth-facade` container the same way** (`./certs:/certs:ro`), with the two paths and
   the (empty) password set directly in `docker-compose.yml` — not `.env`, since there is no secret here to
   manage.

## Consequences

- Closes the third and final gap ADR 0028 named. A login's state token now survives a restart for real:
  verified live by driving `/oidc/login` to create a state token, restarting the `auth-facade` container,
  then driving `/callback` with that same pre-restart `state` value — the logs show OpenIddict
  successfully decrypting the state payload and extracting its PKCE `code_verifier`, proceeding all the
  way to a real token request to OpenEMR (which then rejected the accompanying fake authorization code,
  an expected, unrelated failure — the same one every other live check in this session without real
  OpenEMR credentials has hit).
- AuthFacade can now, in principle, run as more than one replica without a login or an already-established
  session breaking depending on which process handles which request — the actual target ADR 0028's three
  gaps were blocking. This ADR does not itself test or claim multi-replica correctness; it removes the
  last structural blocker named against it.
- Verified with a new unit test that generates two throwaway self-signed PFX files in C#
  (`CertificateRequest.CreateSelfSigned`, no shell-out needed) and drives a full login round trip pointed
  at them, proving the configured branch is genuinely exercised, not just that the app starts — the same
  "prove the other branch too" standard ADR 0043's `AuthFacadeDbTests` already set.
- `scripts/setup/new-authfacade-certs.sh` needs running once, same as `new-dev-cert.sh`, before the first
  `docker compose up` that starts `auth-facade` with `AuthFacadeCertificates` configured — adding it to
  `.env.example`'s absence is deliberate: there is no secret to generate, only files, so `new-env.sh` has
  nothing to do here, and the certs script runs on its own.

## Alternatives considered

- **Persist the OS certificate store OpenIddict's development-certificate helpers already use**, by
  mounting a volume at wherever .NET's cross-platform `X509Store` keeps it on Linux. Rejected: that path is
  an internal implementation detail of .NET's certificate store, not a stable contract to depend on, and
  it would persist the store for *every* certificate a process might ever add to it, not just these two —
  a much larger, vaguer surface than two named files this project owns and controls directly.
- **A password-protected PFX**, generating and storing a passphrase the same way `new-env.sh` generates
  other secrets. Rejected: see Decision 3 — no real confidentiality gain for a dev-only artifact already
  protected by being a git-ignored local file, at the cost of one more secret to manage.

## Related decisions

Closes the third gap [0028](0028-browser-login-and-session-assume-a-single-instance.md) named, the last of
the three; [0043](0043-authfacade-session-state-in-a-dedicated-mariadb-database.md) and
[0044](0044-session-store-backs-the-cookie-for-revocation.md) closed the other two. Mirrors
[0039](0039-gateway-terminates-tls-authfacade-trusts-forwarded-headers.md)'s own file-based, password-less
certificate pattern exactly.
