# Development and Test Environments

OpenEMR Next uses separate environments because code validation, integration
validation, and clinical-data handling have different risk profiles. No
environment described here is approved for production clinical use.

## Environment model

| Environment | Purpose | Data | Access | Required properties |
|---|---|---|---|---|
| Development | Local implementation and debugging | Synthetic data only | Individual contributor | Reproducible, disposable, offline-capable where practical |
| Test | Automated unit, integration, and contract checks | Generated synthetic fixtures | CI and maintainers | Isolated, resettable, deterministic, observable |
| Sanitized integration | Validate coexistence and compatibility with legacy OpenEMR | Approved sanitized snapshot plus synthetic data | Named maintainers and approved stakeholders | Network-isolated, audited, refreshable, rollback-capable |

The environments must not share databases, credentials, encryption keys, queues,
object storage, or service accounts. A lower-trust environment must never have
credentials or network access that can reach a higher-trust environment.

## Development environment

A developer environment must be buildable from a clean checkout using the
pinned SDK in `global.json` and documented project tooling. Setup should be
scriptable and should not require access to clinical systems.

Development configuration must:

- Use local or disposable dependencies.
- Use synthetic identities, patients, facilities, and clinical records.
- Load settings from environment variables or ignored local configuration.
- Provide safe defaults that cannot target production endpoints.
- Make external integrations replaceable with fakes or local test doubles.
- Make it easy to reset databases, queues, and file storage.

Developers must not copy production or live clinical data into local machines,
editor extensions, logs, issue trackers, or test fixtures. Local secrets must
be stored in an approved secret store or ignored developer configuration and
must never be committed.

## Test environment

Automated tests run in isolated resources created for the test run or test
suite. Test data is generated, deterministic, and disposable. Tests must not
rely on the state left by another test or on an external shared environment.

The test environment should provide:

- Unit-test execution without network or database dependencies.
- Integration-test dependencies that can be provisioned and destroyed by CI.
- Contract-test doubles or controlled test endpoints for external systems.
- Database migration and rollback checks.
- Structured logs and test artifacts for failed runs.
- Cleanup even when a test fails.

Tests involving authentication, authorization, audit, encryption, migration,
legacy adapters, or clinical continuity are security-sensitive and require
explicit fixtures for both permitted and denied behavior.

## Sanitized integration environment

The sanitized integration environment is used only when compatibility with
legacy OpenEMR or another external dependency cannot be demonstrated with a
double. It must be provisioned from version-controlled configuration and must
be isolated from production networks and accounts.

Before data enters this environment:

1. Identify the data owner and approve the intended test purpose.
2. Create a repeatable sanitization procedure that removes or irreversibly
   transforms direct and indirect identifiers, credentials, tokens, keys, and
   unnecessary sensitive fields.
3. Validate that the procedure does not preserve usable identity or permit
   re-identification through combinations of fields.
4. Record the source, transformation version, date, approver, and retention
   period.
5. Run a sample review and security check before making the dataset available.

Sanitization is not the same as masking. If a dataset cannot be shown to be
safe for the intended use, use synthetic data instead.

The environment must have:

- Named owners and an access list reviewed at least quarterly.
- Separate credentials and keys that cannot access production.
- Audit logs for data loads, administrative access, and sensitive operations.
- Network controls allowing only approved legacy and integration endpoints.
- A documented refresh, expiry, deletion, and incident-response process.
- Repeatable reset and rollback procedures.
- No outbound email, messaging, billing, or patient-facing side effects unless
  explicitly stubbed or approved for the test.

## Configuration and secrets

Configuration is environment-specific and must be external to the application
binaries. Commit non-sensitive setting names and safe development defaults, but
never commit passwords, tokens, private keys, connection strings containing
credentials, or real endpoint credentials.

Every environment has distinct:

- Identity provider configuration.
- Database and storage credentials.
- Encryption and signing keys.
- Messaging and integration credentials.
- Observability destinations and retention settings.

Secrets are issued with least privilege, rotated, and revoked when access is
removed. Logs must redact tokens, credentials, personal data, and clinical
content by default.

## Promotion and change control

Changes move from development to test to sanitized integration only after the
required automated checks pass. A change that affects authentication,
authorization, data ownership, migrations, clinical workflows, or external
integrations requires explicit review before sanitized integration testing.

Each integration test run records:

- Source commit and configuration version.
- Dataset or fixture version.
- Test scope and result.
- Known discrepancies and their owners.
- Cleanup or retention outcome.

A failed or unsafe integration run stops promotion. The environment owner must
be able to restore the previous known-good version and delete or quarantine
affected data.

## Environment readiness checklist

An environment is ready for use when its owner can demonstrate:

- Clean-checkout setup or provisioning is repeatable.
- Data classification and approved sources are documented.
- Credentials, keys, and network boundaries are separate.
- Reset, refresh, retention, and deletion procedures work.
- Logs and audit records are available without exposing sensitive values.
- Failure, rollback, and incident contacts are documented.
