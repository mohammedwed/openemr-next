# 0015. The development certificate bypass is refused outside Development

- **Status:** Accepted. The Compose stack no longer runs as Development by default; see [0020](0020-local-stack-defaults-are-conservative.md)
- **Date:** 2026-10-23

## Context

`OpenEmr:AcceptAnyServerCertificate` turns off certificate validation for every call to OpenEMR: login and the JWKS fetch. It is needed for OpenEMR's self-signed development certificate.

The setting is read from configuration, not from the environment. `appsettings.Development.json` sets it to `true`, and the Compose stack runs with `ASPNETCORE_ENVIRONMENT=Development`, so the bypass is on in that stack. Nothing stopped the same setting from being enabled in another environment, and the documentation said it applied only in Development. ADR 0011 removed the environment from Infrastructure and Composition, so neither layer can check it.

## Decision

1. **The API host refuses to start** when `OpenEmr:AcceptAnyServerCertificate` is `true` and the environment is not Development. The check is an options validation registered in `AddAuthFacadeHttpDependencies`, so it runs on startup.
2. **Other hosts must add the same check** when they call `AddAuthFacade`. The composition guide says so.
3. **The documentation describes the setting by what it does**, not by the environment it is usually set in.

## Consequences

- Production and staging can't enable the bypass by accident through configuration. A Development environment still can.
- The check depends on the API host. A host that doesn't add it has no protection.
- The bypass has no effect in the Compose stack today, because it reaches OpenEMR over `http://openemr:80`. It applies as soon as the base URL is HTTPS.

## Alternatives considered

- **Check the environment in Composition.** Rejected. ADR 0011 keeps environment out of the layers.
- **Refuse HTTP base URLs when the issuer is HTTPS.** Rejected. The Compose stack depends on HTTP inside its network, and the issuer is the public HTTPS address clients see.
- **Remove the setting and always validate certificates.** Rejected for now. The development stack needs OpenEMR's self-signed certificate.
