# 0028. The browser login flow and the session cookie assume a single AuthFacade instance

- **Status:** Accepted (records an existing trade-off; no code changes)
- **Date:** 2026-10-31
- **Builds on:** [0026](0026-openiddict-client-drives-the-authorization-code-flow.md), [0027](0027-persist-tokens-in-the-session-cookie.md)

## Context

ADR 0026's Consequences already note, in one line, that "no shared Data Protection or OpenIddict-state key
ring exists for multiple instances — the SQLite file and the dev certificates are both per-process." That
line is correct but does not spell out what actually breaks, or why it is not just OpenIddict's problem.
Running more than one AuthFacade replica (any horizontally-scaled deployment, or even two processes behind a
plain round-robin load balancer with no session affinity) breaks the browser flow in three independent ways,
all rooted in the same cause — nothing is shared across processes:

1. **OpenIddict's state-token store.** `/oidc/login` writes a state token to the SQLite file at a
   process-unique temp path (ADR 0026, Decision 2). If the browser's redirect back from OpenEMR reaches a
   different replica for `/callback`, that replica's own SQLite file has no record of the token, and
   OpenIddict rejects the callback outright — not a degraded case, a hard login failure.
2. **OpenIddict's own encryption/signing certificates.** Even if the SQLite file were somehow shared, the
   state token's payload is protected with the dev-only ephemeral certificates ADR 0026 (Decision 9) accepted
   for this stack's maturity level. Those are generated fresh per process, so a different replica could not
   decrypt a state token written by another one regardless of where the row lives.
3. **The `AuthFacadeSession` cookie's own ticket.** `AddDataProtection()` (`AuthFacadeApiExtensions.cs`) is
   registered with no shared key ring (`PersistKeysToFileSystem` to a shared volume, `PersistKeysToDbContext`,
   Redis, or similar) and no fixed application name. By default this means each process generates its own
   key ring, so a session cookie issued by one replica (ADR 0027) cannot be decrypted by another — an
   otherwise-valid, freshly-issued cookie is rejected as if it had been tampered with. This affects ordinary
   `/me` traffic after a successful login, not just the login flow itself.

All three look the same from the outside: a login or an already-logged-in session that fails intermittently,
correlated with which replica happened to handle the request. That is the classic symptom of missing session
affinity, but here it affects a security primitive (an encrypted ticket, and a CSRF/replay check), not just
in-memory application state — a load balancer with sticky sessions hides it, but a rolling deploy that
replaces the instance mid-flow reproduces it even with stickiness in place.

## Decision

1. **Accept this as a known limit of the current scope.** AuthFacade's browser login and session cookie are
   single-instance only. This matches ADR 0020's local-stack scope and the fact that M3 (the gateway, where a
   second replica would first become plausible) has not started (ROADMAP.md).
2. **No code changes accompany this ADR.** It exists to make the operational boundary explicit before any
   future work assumes more than one AuthFacade process.
3. **Record what each failure mode would need before that boundary can move:**
   - A shared Data Protection key ring across every replica (`PersistKeysToDbContext`/`PersistKeysToFileSystem`
     against shared storage, or `PersistKeysToStackExchangeRedis`), covering the `AuthFacadeSession` cookie.
   - Persisted, shared OpenIddict client encryption/signing certificates, replacing the per-process ephemeral
     ones — ADR 0026 (Decision 9) already named this as needed for any real deployment, for a different reason
     (dev-only certs), but it is also a precondition here.
   - A shared backing store for OpenIddict's client state tokens — a real EF Core provider against shared
     storage, replacing the per-process SQLite file.
   - Alternatively, strict session affinity (sticky routing) at whatever sits in front of AuthFacade sidesteps
     all three without removing any of them. Cheaper, but see Alternatives for why it is a stop-gap, not a fix.

## Consequences

- AuthFacade cannot be scaled to more than one replica, and cannot be rolling-deployed mid-flow (a login
  started against one process instance and completed against its replacement fails the same way a second,
  concurrent replica would), until the key ring and state-token store are both externalized.
- This is a stricter constraint than ADR 0026's own note suggests: it is not only OpenIddict's state that
  breaks under multiple instances, but ordinary post-login session validation on `/me`, since the session
  cookie's ticket shares the same missing-key-ring root cause.
- ROADMAP M1's "Persist data-protection keys" item is a precondition for this, not the whole fix — it closes
  gap 3 above but not gaps 1 and 2 (OpenIddict's own store and certificates).
- M3 (gateway) or any deployment work that introduces more than one AuthFacade replica must resolve all of
  1–3 above first, or must enforce session affinity as an interim measure and say so explicitly.

## Alternatives considered

- **Fix it now** (shared key ring, shared OpenIddict store). Rejected for this ADR: there is no current
  requirement to run more than one instance (ADR 0020's scope), and the fix is substantial enough to deserve
  its own ADR once a concrete multi-replica need exists, not speculative work ahead of M3.
- **Sticky sessions as the permanent answer.** Rejected as a full fix, accepted as a interim measure: it
  hides all three failure modes rather than removing them, and a replica restart or rolling deploy still
  reproduces the same failure even with affinity configured, since affinity only controls routing, not what
  survives a process recreation.

## Related decisions

Builds on [0026](0026-openiddict-client-drives-the-authorization-code-flow.md) (the per-process SQLite
state-token store and dev-only certificates) and [0027](0027-persist-tokens-in-the-session-cookie.md) (the
session cookie's Data-Protection-encrypted ticket). Feeds ROADMAP's M1 "Persist data-protection keys" item and
M3 (gateway), the milestone where a second AuthFacade replica first becomes plausible.
