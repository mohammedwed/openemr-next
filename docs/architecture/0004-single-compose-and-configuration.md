# 0004. One Compose file and one configuration source

- **Status:** Accepted
- **Date:** 2026-10-11
- **Source:** Converted from `docs/changelogs/2026-10-11-changelog.md` (decisions 1 and 2)

## Context

The stack has four services and one one-shot seed. They share one database and one set of credentials.

## Decision

All services are defined in one root `docker-compose.yml` in the `openemr-next` project. They read one root configuration file, `.env`, which is generated locally from `.env.example` and gitignored. No service keeps its own copy of a shared value.

## Consequences

- A shared value such as the database password or the client secret cannot drift between services.
- The compose file will grow. Services are grouped with comments.
- Whoever holds `.env` holds every credential, so it is treated as a secret.
- The file should be split when a service needs its own deployment target, not at a fixed service count.
