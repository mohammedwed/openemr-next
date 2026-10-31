# 0025. The seed encrypts the AuthFacade client secret with OpenEMR's own CryptoGen

- **Status:** Accepted
- **Date:** 2026-10-30
- **Amends:** [0005](0005-oauth-client-provisioning-and-scopes.md)

## Context

`scripts/seeds/authfacade-client.sql` wrote `oauth_clients.client_secret` as plain SQL text. OpenEMR's own
`ClientRepository::insertNewClient()` never does this: it encrypts the secret first, with
`(new CryptoGen())->encryptStandard($secret)`, using the site's own encryption keys. ADR 0005 already named
this gap: *"Secret generation should move to a bootstrap that uses OpenEMR's own client-creation code, since
secrets are stored hashed."* It went unnoticed because nothing exercised it: `ClientRepository::validateClient()`
(`src/Common/Auth/OpenIDConnect/Repositories/ClientRepository.php`) checks the secret only for the
`authorization_code` grant —

```php
if ($grantType == 'authorization_code') {
    ...
    if (!empty($clientSecret) && !empty($client['is_confidential'])) {
        $secret = (new CryptoGen())->decryptStandard($client['client_secret']);
        ...
        return hash_equals($clientSecret, $secret);
    }
    return true;
} else {
    // password and refresh grant
    return true;
}
```

ADR 0014 already found that the password and refresh grants skip this check entirely. Building ADR 0026's
authorization-code flow was the first thing in this project ever exercised that `authorization_code` branch,
and it failed every time with `invalid_client` / `Client authentication failed`: `decryptStandard()` tries to
read a version prefix from the stored value (`KeyVersion::fromPrefix`), and a plaintext secret never has one,
so it logs `"Invalid encryption version prefix"` and returns `false` before the comparison ever runs. The
same plaintext write existed in `scripts/stack/apply-credentials.sh`, used when restoring a snapshot.

## Decision

1. **The seed runs as the OpenEMR image, not a bare MariaDB client.** `docker-compose.yml`'s `seed` service
   changes from `image: ${MARIADB_IMAGE}` to `image: ${OPENEMR_IMAGE}`, with `sitesvolume` mounted the same
   way the real `openemr` service mounts it — CryptoGen's keys live there — and runs
   `scripts/seeds/authfacade-client.php` instead of `authfacade-client.sh` / `.sql`.
2. **The new script bootstraps OpenEMR's autoloader and globals before touching CryptoGen**, the same
   bootstrap `bin/console` itself runs (confirmed interactively first: `php -r` with this exact bootstrap,
   then `(new CryptoGen())->encryptStandard(...)`/`decryptStandard(...)`, round-tripped correctly). It still
   polls for `oauth_clients` to exist first (up to 15 minutes, as the old shell script did) using a plain PDO
   connection — the bootstrap is deferred until the schema is known to exist, not attempted up front.
3. **A full OpenEMR console command was not written for this.** `RegisterApiTestClientCommand`
   (`src/Common/Command/`) already does something close, but adding a sibling command means registering a
   new class inside OpenEMR's own source tree — forking the "unmodified, separately deployed" system ADR
   0001 requires. The seed script calls `CryptoGen` directly instead, reusing OpenEMR's real encryption
   without adding to its codebase.
4. **The seed still dictates `client_id`, `client_secret` and `redirect_uri` from `.env`**, unlike
   `RegisterApiTestClientCommand`, which generates its own. Every other setting that already reads
   `AUTHFACADE_CLIENT_ID` / `AUTHFACADE_CLIENT_SECRET` (docker-compose, `apply-credentials.sh`, AuthFacade's
   own `OpenEmr:*` options) keeps working unchanged.
5. **`apply-credentials.sh` gets the same fix**, for the same reason: it writes to the same column after a
   snapshot restore. It already ran a throwaway `docker run --entrypoint php` container against the sites
   volume for the `sqlconf.php` rewrite; a second such container, running the same bootstrap-and-encrypt
   steps as the seed script, produces the ciphertext the subsequent `UPDATE oauth_clients` statement now uses.
6. **Idempotency is preserved.** Both the insert-if-absent and the always-run update (scope, redirect URI,
   now also secret and `is_confidential`) match the old SQL's behavior; re-running the seed keeps encrypting
   and writing the same configured secret, not generating a new one each time.

## Consequences

- The authorization-code grant's client authentication (ADR 0014, ADR 0026) now actually works; it was
  silently broken from ADR 0005 onward.
- The seed and `apply-credentials.sh` both depend on the OpenEMR image being able to run `php` against the
  sites volume outside the `openemr` service itself — already true, and already exactly how
  `apply-credentials.sh` handled `sqlconf.php`.
- `scripts/seeds/authfacade-client.sh` and `.sql` are removed outright, not kept dormant: they were not an
  alternative design, they were a bug.
- The seed's database wait loop moved from `mariadb -e` in shell to a plain `PDO` connection in PHP; the
  15-minute/90-attempt timeout and its log messages are otherwise unchanged.

## Alternatives considered

- **Use `RegisterApiTestClientCommand` as-is.** Rejected. It generates its own `client_id` and
  `client_secret` rather than accepting ours, which would mean threading a generated value back out of the
  container into `.env` and every place that reads `AUTHFACADE_CLIENT_ID`/`AUTHFACADE_CLIENT_SECRET` today —
  a much larger change than the actual bug warranted.
- **Add a new OpenEMR console command for this.** Rejected. It would mean maintaining a class inside
  OpenEMR's own source tree, which ADR 0001 rules out; calling `CryptoGen` directly needs no such addition.
- **Replicate `CryptoGen`'s encryption algorithm in raw SQL/shell.** Rejected. It depends on the site's own
  key material (`collectCryptoKey`, drive-sourced), which is exactly the thing that makes this correct only
  when OpenEMR's own code does it; reimplementing it outside OpenEMR would be fragile and could drift from
  whatever OpenEMR's encryption scheme becomes next.

## Related decisions

Fixes the gap ADR 0005 already named. Makes ADR 0014's analysis of `validateClient()` actionable instead of
just informative. Was found while building ADR 0026, which needed the `authorization_code` grant to actually
authenticate.
