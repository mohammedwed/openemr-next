# 0005. The AuthFacade OAuth client is provisioned by seed with minimal scopes

- **Status:** Accepted; the secret-storage gap named in Consequences is fixed by [0025](0025-seed-encrypts-the-client-secret.md)
- **Date:** 2026-10-10 and 2026-10-11
- **Source:** Converted from `docs/changelogs/2026-10-10-changelog.md` (decision 7) and `docs/changelogs/2026-10-11-changelog.md` (decisions 4 and 5)

## Context

AuthFacade needs an OAuth client in OpenEMR. Registration through OpenEMR's form had a bug when system scopes were selected. Other clients in the development database hold broad `user/*` and `system/*` scopes.

## Decision

- A one-shot seed inserts the client into `oauth_clients` if absent and updates its scope if present. It waits for the schema, not for OpenEMR to serve traffic.
- The client holds authentication scopes only: `openid`, `profile`, `email`, `offline_access`. It holds no API resource scopes.

## Consequences

- A leaked AuthFacade secret cannot read patient data through the FHIR or REST API.
- `/me` serves identity claims only. Calling the OpenEMR API later requires adding scopes one resource at a time.
- The seed does not reconcile `grant_types`. An existing row needs a manual change or a fresh volume. This is a known gap.
- Secret generation should move to a bootstrap that uses OpenEMR's own client-creation code, since secrets are stored hashed.
