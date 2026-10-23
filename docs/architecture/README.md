# Architecture decision records

Decisions that shape the system, one per file, numbered in order. A decision is
never edited to change its outcome; a new record supersedes the old one and the
old one's status is updated to point to it.

| ADR | Title | Status | Date |
|---|---|---|---|
| [0001](0001-authfacade-as-token-broker.md) | AuthFacade is a token broker in front of OpenEMR | Accepted | 2026-10-10 |
| [0002](0002-facade-issued-tokens.md) | AuthFacade issues its own tokens | Accepted | 2026-10-10 |
| [0003](0003-delegate-credential-verification-to-openemr.md) | Credential verification is delegated to OpenEMR | Accepted | 2026-10-10 |
| [0004](0004-single-compose-and-configuration.md) | One Compose file and one configuration source | Accepted | 2026-10-11 |
| [0005](0005-oauth-client-provisioning-and-scopes.md) | The AuthFacade OAuth client is provisioned by seed with minimal scopes | Accepted | 2026-10-10 |
| [0006](0006-black-box-integration-tests.md) | Integration tests run black-box against the running stack | Accepted | 2026-10-11 |
| [0007](0007-layered-architecture-with-enforced-dependencies.md) | AuthFacade is split into layered projects with enforced dependency rules | Proposed | 2026-10-21 |
| [0008](0008-login-credential-rules.md) | Login accepts any credentials OpenEMR could hold; OpenEMR decides correctness | Proposed | 2026-10-21 |
| [0009](0009-password-plaintext-retrieved-once.md) | The password plaintext is retrieved once and cleared | Proposed | 2026-10-21 |
| [0010](0010-separate-contracts-project.md) | AuthFacade HTTP contracts live in the API project; ports live in their own project | Proposed | 2026-10-23 |
