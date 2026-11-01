# 0008. Login accepts any credentials OpenEMR could hold; OpenEMR decides correctness

- **Status:** Superseded by [0035](0035-password-grant-and-bearer-auth-are-removed.md)
- **Date:** 2026-10-21

## Context

A first version of the value objects enforced the password policy for new accounts: 8 to 128 characters, no whitespace, with lowercase, uppercase, digit and symbol. At login that rejects existing OpenEMR accounts that predate the policy, and the user would see `invalid_request` with the correct password.

## Decision

Login checks only what can be stored and what is safe to process:

- **Username:** 1 to 255 characters, no control characters. The 255 limit matches `users.username` in OpenEMR's schema.
- **Password:** 1 to 1024 characters. OpenEMR's schema sets no plaintext limit because it stores a hash, so the 1024 limit is an input guard, not a legacy rule.

Whether the credentials are correct is decided by OpenEMR.

## Consequences

- Existing accounts with short or simple credentials can sign in.
- Oversized input is rejected before reaching OpenEMR.
- The complexity policy is not enforced anywhere yet. It belongs on a separate value object used when accounts are created or passwords changed. No such flow exists in this service.
- Integration tests cover a short legacy username and an oversized password. See [0006](0006-black-box-integration-tests.md).
