# Changelog: 2026-10-11

Architecture changes to the OpenEMR-Next solution: how the local stack is
composed, how AuthFacade authenticates against OpenEMR, and how the system is
verified end to end.

## Architecture at the end of the day

```
            +-------------------+
 client --> | AuthFacade (.NET) |  issues its own JWT (Bearer)
            +---------+---------+
                      | password grant (OAuth2)
            +---------v---------+        +-----------+
            | OpenEMR (legacy)  |<------>| MariaDB   |
            +-------------------+        +-----------+
                                               ^
            seed (one-shot) registers the OAuth client
```

Everything runs in one compose project (`openemr-next`) on a single network.
Services address each other by name. The database is shared; OpenEMR owns the
schema and the OAuth server; AuthFacade is a token broker in front of it.

## Decisions

### 1. One solution-level stack, not per-service compose files

Compose files were consolidated into one root file. The reasoning:

- The stack is small (four services plus one-shot seed), and the services share
  one database and one set of credentials. Splitting would duplicate that wiring.
- A single file gives one place to see dependencies, ports and volumes.
- The split can be revisited when a service has its own deployment target. Until
  then, the cost of splitting exceeds the benefit.

Trade-off: the file will grow as services are added. Services are grouped by
ownership with comments so the file stays navigable.

### 2. One configuration source for the whole solution

All services read one root configuration file. No service keeps a private copy
of a value it shares with another.

- A value shared by two services (the database password, the client secret)
  cannot drift between them.
- Secrets are generated locally rather than committed. The template holds
  placeholders only, and a generator fills them.

Trade-off: the file is a single point of configuration. Anyone with it has every
credential, so it is gitignored and treated as a secret.

### 3. AuthFacade is a token broker, not an identity provider

AuthFacade takes a username and password, exchanges them with OpenEMR's
password grant, and issues its own short-lived JWT. It does not store passwords
or user records.

- **Identity stays in OpenEMR.** OpenEMR already owns users, ACLs and password
  hashing. Rebuilding them would duplicate a security-critical surface.
- **AuthFacade's token is its own.** Clients validate AuthFacade's signature,
  issuer and audience. They never hold OpenEMR's token, so the upstream token
  stays inside the service boundary.
- **The subject comes from the upstream token**, so the identity AuthFacade
  asserts is the one OpenEMR authenticated.

Trade-off: the password grant is the least preferred OAuth flow, because the
client handles the user's password. It is acceptable here because AuthFacade is
the only client, runs on the internal network, and is a development stand-in
for the browser flow. The browser authorization-code flow remains the
production target and is not implemented.

### 4. OpenEMR's OAuth client is provisioned by data, not code

AuthFacade's client registration is an insert into OpenEMR's `oauth_clients`
table, performed by a one-shot seed that runs after the schema exists. The seed
is idempotent: it inserts when absent and updates scope when present.

- The client is environment configuration, not application code.
- The seed waits for the schema, not for OpenEMR to serve traffic. Those are
  separate conditions, and the readiness gate (decision 6) covers the second.

Trade-off: the seed does not reconcile every field. It updates scope but not
`grant_types`, so an existing row needs a manual change or a fresh volume.
This is a known gap.

### 5. Privilege boundary: minimal scopes for the AuthFacade client

The AuthFacade client is granted authentication scopes only (`openid`,
`profile`, `email`, `offline_access`) and no API resource scopes. Other clients
in the dev database hold broad `user/*` and `system/*` scopes.

- A leaked AuthFacade secret cannot read patient data through the FHIR or REST
  API.
- AuthFacade's own `/me` endpoint serves identity claims only.

Trade-off: when AuthFacade needs to call the OpenEMR API, scopes are added
deliberately, one resource at a time, not copied from a broad client.

### 6. Test strategy is black-box, against the running stack

Two test projects with different scopes:

- **`Db.Tests`** starts a throwaway MariaDB from the dump and checks the schema
  and seed data. It proves the baseline is reproducible and independent of any
  running environment.
- **`AuthFacade.Integration.Tests`** runs against the live compose stack. It
  checks the seed, OpenEMR's OAuth server, the password grant, and the protected
  `/me` endpoint, including signature validation.

A **readiness gate** runs before the integration tests. It checks four
conditions (database, seed, OpenEMR readiness, AuthFacade) under one deadline.
Tests never run against a half-started stack, so failures reflect the code
under test rather than startup timing.

Trade-off: the integration suite depends on Docker and a long first start. It is
slow and cannot run in a unit-test-only pipeline. It is kept separate, and its
timeout differs between warm and cold runs.

### 7. Build isolation for AuthFacade

AuthFacade builds inside a Linux container from its own project folder. Local
build output and developer secrets are excluded from the build context.

- The image contains only what the service needs to run.
- Restore and publish happen in the target OS, so host-specific package paths
  cannot leak into the image.

## Known architectural limits

- **OpenEMR's failed-login path is defective.** On some wrong-password requests,
  OpenEMR's failed-login counter receives an object where a string is expected and
  returns 500. AuthFacade cannot prevent this from the outside. It is tracked
  upstream, and the strict test will fail if it recurs.
- **Start-up ordering is incomplete.** AuthFacade waits for the database to be
  healthy but not for OpenEMR, so it can start before OpenEMR serves requests.
  Requests in that window fail, and only the readiness gate hides them in tests.
- **Bind-mounted source is slow on Windows.** OpenEMR's first start performs a
  recursive permission pass across the bind-mounted tree. This is an environment
  cost rather than a design flaw, but it sets the length of a cold start.
- **The password grant is deprecated in OAuth 2.1.** It is retained for this
  integration only.

## Open architectural questions

- **Where does the browser login live?** If users must sign in through a web page,
  AuthFacade needs the authorization-code flow with PKCE, and the redirect and
  consent path must be designed.
- **Which context owns the OAuth client?** Today OpenEMR owns the client row and
  AuthFacade consumes it. If AuthFacade becomes a standalone identity service,
  ownership would move, and so would the seed.
- **Should dev seed data be excluded from images?** The seed contains the client's
  scope list, which is environment configuration. Excluding it keeps dev data out
  of build artifacts.
- **When to split the compose file.** The threshold is a service that needs its
  own deployment, not a service count.

## Security posture

- Credentials exposed during development were rotated and replaced with generated
  values. The old values are treated as compromised.
- Secrets are held in one gitignored file and never committed.
- Broad API scopes on other dev clients remain a risk and should be removed from
  the baseline before the database is shared.