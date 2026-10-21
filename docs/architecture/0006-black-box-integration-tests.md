# 0006. Integration tests run black-box against the running stack

- **Status:** Accepted
- **Date:** 2026-10-11
- **Source:** Converted from `docs/changelogs/2026-10-11-changelog.md` (decision 6)

## Context

The behavior that matters spans OpenEMR, its database, the seed and AuthFacade. Mocks would not show whether the real login works.

## Decision

Integration tests (`OpenEmrNext.AuthFacade.IntegrationTests`) run against the live compose stack over HTTP. A readiness gate checks the database, the seed, OpenEMR and AuthFacade under one deadline before any test runs.

## Consequences

- Failures reflect the code under test, not startup timing.
- The suite needs Docker and a long first start, so it cannot run in a unit-test-only pipeline. It is kept separate from the unit tests, which run without Docker.
- Failures caused by OpenEMR defects appear here. The strict failed-login test will fail if the upstream defect recurs.
