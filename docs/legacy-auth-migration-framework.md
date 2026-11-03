# Framework: migrating legacy authentication

This is a reusable framework for migrating authentication off a legacy system,
derived from this project's own evidence: the ~47 ADRs under
[`docs/architecture/`](architecture/README.md) and the changelogs under
[`docs/changelogs/`](changelogs/) that trace AuthFacade from a password-grant
token broker to the identity bridge described in
[ADR 0047](architecture/0047-the-identity-bridge-design.md). Every claim below
cites the ADR it came from. Where this project hit a fork in the road, that
fork is generalized into a question or decision graph other teams can reuse.

This is not OpenEMR-specific advice. It is a framework for the general
problem: *a legacy system owns identity, and something new needs to sit next
to or in front of it without becoming a second, divergent identity provider.*
Two companion documents exist:

- [`legacy-auth-facade-framework.md`](legacy-auth-facade-framework.md) — a
  standalone version of this same framework, every pattern restated
  generically (no ADR citations, no file paths, no OpenEMR/.NET-specific
  names), written for readers new to the problem. Hand that one to a team
  working in a different stack, or outside this repository entirely.
- [`auth-facade-implementation-guide.md`](auth-facade-implementation-guide.md)
  — a sequential "do this, then that" build order for developers: ten
  phases, each step with a verifiable "done when" and the common mistakes
  specific to it. Use that one while actually building.

**Scope.** This framework covers the facade/bridge service itself —
delegated credential verification, session and token handling, the
hardening/testing/CI/container concerns around that one component. It is
*not* a framework for a full legacy-auth migration end to end. Explicitly
out of scope, and not addressed anywhere below:

- **Authorization** beyond authentication — what a signed-in identity may
  do with legacy or new-service data. Named repeatedly below (§5, §11) as a
  separate, unbuilt layer, never assumed solved by anything the bridge does.
- **User/account migration** — moving, merging, or deduplicating accounts
  into a new system of record. This framework's bridge never becomes the
  account store (§3.4); if yours does, that migration needs its own plan.
- **MFA, password reset, account recovery, and other identity-lifecycle
  flows** — none exist in the case study this framework is drawn from.
- **The broader domain-service migration** (bounded-context carving,
  per-service databases, anti-corruption adapters, event publishing) — a
  separate concern with its own document (`docs/domain-driven-design.md`,
  `docs/openemr-ddd-design.md`), only touched here where it intersects with
  identity (§5, §9.6).

## How this project answered the general problem (case study)

| Stage | What existed | What changed | ADR |
|---|---|---|---|
| 1. Stand-in broker | Nothing | `/connect/token` password grant, AuthFacade issues its own JWT | [0001](architecture/0001-authfacade-as-token-broker.md), [0002](architecture/0002-facade-issued-tokens.md) |
| 2. Pass-through | Facade-issued JWT | Facade returns OpenEMR's own token response unchanged | [0012](architecture/0012-accept-openemr-tokens-by-token-exchange.md), [0013](architecture/0013-pass-openemr-tokens-through.md) |
| 3. Real browser flow | Password grant (credentials touch the client) | Authorization-code + PKCE against OpenEMR's own OIDC server | [0024](architecture/0024-browser-authorization-code-flow-with-pkce.md), [0026](architecture/0026-openiddict-client-drives-the-authorization-code-flow.md) |
| 4. BFF, not broker | Token handed to the browser | Session cookie only; real token kept server-side | [0027](architecture/0027-persist-tokens-in-the-session-cookie.md), [0029](architecture/0029-yarp-proxies-browser-sessions-to-openemrs-api.md) |
| 5. Stand-in retired | Password grant still present, unused | Removed outright, along with bearer auth and 3 of 5 layered projects | [0035](architecture/0035-password-grant-and-bearer-auth-are-removed.md) |
| 6. Hardened bridge | Session cookie with no server-side record | Revocable, restart-survivable, auto-refreshing, audited | [0043](architecture/0043-authfacade-session-state-in-a-dedicated-mariadb-database.md)–[0046](architecture/0046-persisted-openiddict-certificates.md) |
| 7. Capstone | ~20 scattered decisions | One document describing guarantees + accepted risks | [0047](architecture/0047-the-identity-bridge-design.md) |

The throughline: **the stand-in that unblocks early work (a password grant
with a facade-issued token) is explicitly temporary, named as such at the
start, and actually deleted once the real flow exists** — not left running
in parallel "just in case." See [0035](architecture/0035-password-grant-and-bearer-auth-are-removed.md),
Alternatives considered, for how "keep it for a future mobile/M2M client" was
evaluated and rejected rather than defaulted into.

---

## 1. Questions to ask the customer / stakeholders

Ask these before any architecture discussion. Most of this project's rework
(ADR 0001 → 0035, six ADRs' worth of layered architecture built then deleted)
traces back to answering the "do we need this" questions late instead of
first.

**Scope and ownership**
- Who today can answer "is this username/password combination correct"? Is
  there more than one such system (e.g., a second IdP for a subset of users)?
- Does the legacy system expose a standards-based protocol (OAuth2/OIDC,
  SAML) already, or only a proprietary login form/table? (Determines whether
  you delegate or must build verification yourself — see
  [0003](architecture/0003-delegate-credential-verification-to-openemr.md).)
- Who owns the user/credential data after migration — does it move, get
  duplicated, or stay put permanently? ("Stays put permanently" is a valid,
  durable answer; see [0003](architecture/0003-delegate-credential-verification-to-openemr.md),
  reaffirmed in principle by [0047](architecture/0047-the-identity-bridge-design.md).)
- Is there a compliance or audit regime that constrains where credentials or
  tokens may be logged, stored, or transit? (Shaped
  [0042](architecture/0042-audit-log-scoped-to-login-and-the-bff-proxy.md) and the
  "credentials never logged" requirement throughout.)

**Client population**
- Which clients need to authenticate today: browser only, native mobile,
  server-to-server (M2M), third-party integrations? Get this in writing —
  this project explicitly rejected building for hypothetical mobile/M2M
  clients until one actually exists ([0035](architecture/0035-password-grant-and-bearer-auth-are-removed.md)).
- For each client type, does it need to see the legacy system's token at all,
  or only an authenticated session with the new frontend?

**Operational constraints**
- Will this run as one instance or must it scale horizontally / survive
  rolling deploys from day one? This changes the session-storage answer
  entirely ([0028](architecture/0028-browser-login-and-session-assume-a-single-instance.md)).
- What is acceptable behavior when the legacy system is slow or down: fail
  closed, fail open, or degrade a specific feature? (Drove
  [0017](architecture/0017-circuit-breakers-guard-openemr-calls.md) /
  [0041](architecture/0041-circuit-breakers-guard-the-remaining-openemr-calls.md).)
- What is the actual password/credential policy already in production? Do
  not assume a clean policy — existing accounts may predate any policy at all
  ([0008](architecture/0008-login-credential-rules.md)).
- Is there a path to revoke a compromised session *today*, without legacy
  system cooperation? Who is allowed to revoke someone else's session, and
  how is that authorized? ([0044](architecture/0044-session-store-backs-the-cookie-for-revocation.md),
  [0045](architecture/0045-silent-refresh-and-admin-session-revocation.md).)

**Timeline pressure**
- Is there a deadline that justifies a known-temporary stand-in (e.g., a
  grant type you intend to delete later)? If yes, write the deletion
  criterion down *now*, before building the stand-in — this project named the
  password grant's removal condition in [0001](architecture/0001-authfacade-as-token-broker.md)
  three weeks before [0035](architecture/0035-password-grant-and-bearer-auth-are-removed.md)
  actually removed it.

---

## 2. Architectural questions to resolve early

Group these the way this project's ADRs fell naturally into groups — resolving
one early tends to answer several others for free.

### Identity ownership
- Does the new component **verify** credentials (delegate to legacy) or
  **issue** its own identity assertions? Delegating was this project's
  answer and it held for the whole migration ([0003](architecture/0003-delegate-credential-verification-to-openemr.md)).
- Is the new component a **token broker** (hands a token to any caller), a
  **BFF** (holds the token server-side, gives browsers a cookie only), or a
  **dedicated identity service** (becomes the system of record itself)? This
  project moved broker → BFF ([0001](architecture/0001-authfacade-as-token-broker.md) →
  [0035](architecture/0035-password-grant-and-bearer-auth-are-removed.md)) and left
  "BFF vs. dedicated service, permanently" as an open ADR gate at M3 (see
  §3 Decision graphs, below, and ROADMAP.md's M3 decision gate).

### Session model
- Where does session state actually live: a self-decrypting cookie
  (stateless, cannot be revoked without a blocklist) or a server-side record
  referenced by an opaque cookie key (stateful, revocable)? This project
  started with the former implicitly ([0027](architecture/0027-persist-tokens-in-the-session-cookie.md))
  and moved to the latter once revocation became a requirement
  ([0043](architecture/0043-authfacade-session-state-in-a-dedicated-mariadb-database.md),
  [0044](architecture/0044-session-store-backs-the-cookie-for-revocation.md)).
- Does the browser ever hold a real, legacy-system-recognized token, or only
  an opaque session reference? Letting the browser hold the real token is the
  single biggest blast-radius decision in the whole migration — this project
  decided *never* ([0047](architecture/0047-the-identity-bridge-design.md), "The
  browser is never, under any path, handed a bearer token").

### Token/credential handling
- If credentials must pass through the new component at all (e.g., a grant
  type that takes a password), what is the retrieved-once-and-cleared
  discipline, and is it tested? ([0009](architecture/0009-password-plaintext-retrieved-once.md).)
  Prefer avoiding this entirely via a redirect-based flow where the new
  component never sees the password ([0024](architecture/0024-browser-authorization-code-flow-with-pkce.md)).
- Does the legacy system's own token survive a refresh transparently to the
  client, or does the client have to notice and re-authenticate? Silent,
  server-side refresh with a safe fallback (serve the stale token, let the
  legacy system's own 401 decide) kept this invisible to the browser
  ([0045](architecture/0045-silent-refresh-and-admin-session-revocation.md)).

### Multi-instance / deployment
- Everything that is "per-process" today (an ephemeral cert, a local SQLite
  file, an in-memory key ring) is a hidden single-instance assumption. Audit
  for all three before scaling past one replica:
  state-token store, encryption/signing certificates, and the session
  ticket's own key ring ([0028](architecture/0028-browser-login-and-session-assume-a-single-instance.md)).

### Data ownership and boundaries
- Which database(s) hold what? This project kept the legacy database
  untouched and gave the bridge its own database for session state only —
  two databases, two least-privilege users, no shared tables
  ([0043](architecture/0043-authfacade-session-state-in-a-dedicated-mariadb-database.md)).

---

## 3. Decision graphs

### 3.1 How should the new component relate to the legacy identity system?

```text
Does the legacy system already speak a standard protocol (OIDC/OAuth2/SAML)?
│
├─ No → Will you be allowed to modify the legacy system?
│        ├─ Yes → Add a standard protocol endpoint to it (smaller blast radius
│        │        than building a parallel identity system).
│        └─ No  → Build a verification adapter in front of it (delegate
│                 credential checks; do not duplicate the credential store).
│                 See 0003.
│
└─ Yes → Do any clients need the legacy system's own token (bearer, API
          calls as that identity)?
          ├─ No  → Build a BFF: redirect-based login, session cookie only,
          │        token kept server-side. See 0024, 0026, 0027, 0029.
          │        This is the default answer — prefer it.
          └─ Yes → Pass the legacy token through unchanged rather than
                    minting your own (0013), AND still keep it off the
                    browser if the caller is itself a browser-facing app
                    (0029's BFF proxy pattern: attach the token server-side
                    on the hop that actually needs it).
```

### 3.2 Where does session state live?

```text
Must a session be revocable before natural expiry (compromise response,
admin action, logout-everywhere)?
│
├─ No, and single-instance is acceptable for the foreseeable scope
│   → Self-contained encrypted cookie is acceptable short-term.
│     Write down this limit explicitly (0028) so nobody assumes revocation
│     exists when it does not.
│
└─ Yes, OR you need >1 instance / rolling deploys
    → Server-side session record, opaque cookie key only.
      Needs: shared backing store, shared Data Protection / encryption key
      ring, persisted (not ephemeral) certs. See 0043, 0044, 0046.
      Revocation = delete the row (0044). No legacy-system cooperation
      required for your own side of the token.
```

### 3.3 Is it time to delete the stand-in?

```text
Does anything in the running system still call the stand-in path
(e.g., a password grant, a legacy direct-login form) in production traffic?
│
├─ Yes → Not yet. Keep it, but keep its removal criterion written down
│         (0001's "deprecated... kept only for this integration" framing).
│
└─ No  → Is the reason you are keeping it a *real, scheduled* need
          (a client that exists today) or a *hypothetical future* one?
          ├─ Hypothetical (e.g., "maybe mobile will need it", "maybe M2M
          │   later") → Delete it now. A real future need gets its own ADR
          │              against what's actually required then — see 0035's
          │              Alternatives considered. Keeping unused auth surface
          │              "just in case" is attack surface with no owner.
          └─ Real, scheduled → Keep it, scoped to that client only, and
                                revisit at the date it was scheduled for.
```

### 3.4 BFF forever, or a dedicated identity service?

This is the open decision this project deferred to its own M3 gate
(ROADMAP.md: "ADR: whether AuthFacade stays as the permanent legacy bridge or
is replaced by a dedicated identity service") — generalized here because most
legacy-auth migrations hit the same fork eventually.

```text
Does the bridge need to become the system of record for identity (own
accounts, own password reset, own MFA enrollment) rather than only brokering
sessions against the legacy system?
│
├─ No → Stay a BFF/bridge. Cheaper, smaller attack surface, no duplicated
│        user data. Revisit only if a concrete requirement forces the
│        question (e.g., legacy system is being decommissioned, or a
│        capability the legacy system cannot support is required: SSO
│        federation, per-tenant IdP, step-up auth).
│
└─ Yes → Dedicated identity service. Needs: a migration plan for existing
          credentials (hash format compatibility or forced reset), a
          decision on who owns the OAuth client registration going forward
          (an open question this project flagged explicitly — ROADMAP.md,
          "Ownership of the OAuth client"), and a cutover plan for sessions
          already issued by the bridge.
```

---

## 4. Hardening requirements

Treat this as a checklist of *categories*, each backed by a concrete decision
in this project. A migration that skips a category should say so explicitly
as an accepted risk (§6), not silently.

| Category | Requirement | Evidence |
|---|---|---|
| Transport | TLS terminated at a trusted boundary; redirect HTTP; HSTS; forwarded-header trust scoped to a known network, not blindly trusted | [0039](architecture/0039-gateway-terminates-tls-authfacade-trusts-forwarded-headers.md) |
| Credential handling | Never log credentials or tokens, at any log level, in any logger category; retrieve plaintext once and clear it if you must touch it at all | project-wide; [0009](architecture/0009-password-plaintext-retrieved-once.md) |
| Rate limiting | Every unauthenticated entry point (login, callback) shares a budget per client address | [0018](architecture/0018-rate-limit-the-login-endpoint-per-client-address.md), [0038](architecture/0038-rate-limit-login-and-callback-per-client-address.md) |
| Resilience | Circuit breakers around every outbound call to the legacy system (token exchange, key fetch, proxied calls) so one failing dependency degrades fast instead of hanging every caller | [0017](architecture/0017-circuit-breakers-guard-openemr-calls.md), [0041](architecture/0041-circuit-breakers-guard-the-remaining-openemr-calls.md) |
| Session integrity | Cookie carries an opaque reference only; the real ticket (and any real legacy token) lives server-side | [0027](architecture/0027-persist-tokens-in-the-session-cookie.md), [0043](architecture/0043-authfacade-session-state-in-a-dedicated-mariadb-database.md) |
| Revocation | A session can be killed server-side, both self-service and admin-initiated, with no cooperation from the holder of the cookie | [0044](architecture/0044-session-store-backs-the-cookie-for-revocation.md), [0045](architecture/0045-silent-refresh-and-admin-session-revocation.md) |
| Logout | Logout ends the session on *both* sides when the legacy system supports RP-initiated logout — read its actual contract from source/discovery, don't assume | [0040](architecture/0040-rp-initiated-logout.md) |
| Key/cert durability | Nothing security-critical is ephemeral-per-process: encryption keys, signing certs, and state-token stores all survive a restart | [0043](architecture/0043-authfacade-session-state-in-a-dedicated-mariadb-database.md), [0046](architecture/0046-persisted-openiddict-certificates.md) |
| Audit | Login outcomes and any proxied data access are logged to one filterable category, with zero credentials/tokens/bodies in it | [0042](architecture/0042-audit-log-scoped-to-login-and-the-bff-proxy.md) |
| Start-up ordering | The new component refuses to serve traffic before its dependencies (legacy system readiness, client registration, schema) are confirmed ready | [0036](architecture/0036-wait-for-openemrs-readiness-probe-at-startup.md), [0037](architecture/0037-authfacade-depends-on-seed-completing.md) |
| Input limits | Bound every field to what the legacy schema can actually hold, as a guard, not a policy statement | [0008](architecture/0008-login-credential-rules.md) |
| Attack surface | Remove auth surface nothing calls (grant types, bearer schemes) instead of leaving it live-but-unused | [0035](architecture/0035-password-grant-and-bearer-auth-are-removed.md) |

---

## 5. Microservice considerations

These apply once there is more than one backend service behind the same
login, which is the scenario this project is deliberately building toward
(ROADMAP.md M3–M6) but has not reached yet — so treat this section as
forward-looking, flagged as such.

- **One externally-reachable identity surface.** Every consumer — the
  dashboard and the identity bridge's own login/callback/session endpoints —
  shares one address, fronted by a gateway that makes no identity decisions
  of its own ([0033](architecture/0033-gateway-fronts-webapp-and-authfacade.md)).
  Don't let each service grow its own login entry point.
- **Token validation belongs at the edge, once, not in every service.** This
  project named this explicitly as unbuilt-but-planned: "Token validation at
  the gateway (AuthFacade currently validates its own tokens on `/me` only)"
  (ROADMAP.md, M3). Validating independently in N services is N places to get
  it wrong and N places to update when the signing key rotates.
- **The BFF proxy pattern scales by adding routes to a gateway, not by
  learning every service by name.** Decided ahead of the need
  ([0030](architecture/0030-bff-proxy-forwards-to-the-gateway-not-each-service.md)):
  a browser session's calls go through one proxy cluster pointed at the
  gateway; new services arrive behind that gateway, not as new proxy
  destinations the BFF must know individually.
- **Service-to-service calls need their own answer — don't reuse the
  browser's session mechanism for them.** This project explicitly deferred
  M2M to "its own ADR... against whatever OpenEMR actually supports then"
  rather than stretching the browser flow to cover it
  ([0035](architecture/0035-password-grant-and-bearer-auth-are-removed.md)).
  A session cookie is not a service credential; don't let one become one by
  default.
- **Authorization is a separate, not-yet-answered layer from authentication.**
  This project's own accepted-risk list is explicit that identity bridge work
  answers "who is this" but not "what may they do with service X's data" —
  call this out as its own workstream per service, not an extension of the
  identity bridge ([0047](architecture/0047-the-identity-bridge-design.md),
  Consequences → Accepted risks).
- **Each new service inherits the bridge's accepted risks until it actually
  needs the gap closed** ([0047](architecture/0047-the-identity-bridge-design.md),
  Consequences) — e.g., multi-instance support, OpenEMR-side revocation. Don't
  silently assume a gap closed just because time has passed; re-check it
  against the specific service's requirements.
- **Scope widening is explicit, not incidental.** The browser's OIDC scope
  intentionally does not include API access scopes the legacy system
  requires for proxied calls until a real caller needs them
  ([0047](architecture/0047-the-identity-bridge-design.md)) — widen scope
  per concrete need, not preemptively "to be safe for later."

---

## 6. Tradeoffs

| Choice | Pro | Con | This project's call |
|---|---|---|---|
| Token broker (mint your own token) vs. pass-through legacy token | Broker: stable contract for clients regardless of legacy quirks | Broker: you now own token semantics, expiry, and signing — a second source of truth for identity | Started as broker ([0002](architecture/0002-facade-issued-tokens.md)), moved to pass-through ([0013](architecture/0013-pass-openemr-tokens-through.md)), then removed entirely in favor of a BFF ([0035](architecture/0035-password-grant-and-bearer-auth-are-removed.md)) |
| BFF (cookie only) vs. handing clients a bearer token | BFF: browser never holds a credential worth stealing via XSS; smaller client attack surface | BFF: every browser call must round-trip through the BFF/proxy; no direct client-to-legacy-API calls | BFF, unconditionally, for browser clients ([0047](architecture/0047-the-identity-bridge-design.md)) |
| Stateless cookie vs. server-side session record | Stateless: no database, scales trivially | Stateless: no revocation without a separate blocklist; same problem resurfaces the moment you need revocation | Moved stateless → server-side once revocation became a real requirement ([0043](architecture/0043-authfacade-session-state-in-a-dedicated-mariadb-database.md)) |
| Sticky sessions vs. shared state for multi-instance | Sticky: cheap, no new infrastructure | Sticky: a rolling deploy or replica failure reproduces the exact failure it was meant to hide — routing, not state, is what it fixes | Rejected as the permanent answer; accepted only as an interim measure if ever needed ([0028](architecture/0028-browser-login-and-session-assume-a-single-instance.md)) |
| Keep a deprecated grant "just in case" vs. delete once unused | Keep: zero risk of breaking a client you didn't know about | Keep: permanent attack surface and test burden for a caller that may never materialize | Delete, with a real future need getting its own ADR instead of a standing justification ([0035](architecture/0035-password-grant-and-bearer-auth-are-removed.md)) |
| Build your own circuit breaker vs. a library pattern reused later | Reuse: the pattern survives even when the call site it guarded is deleted | Reuse: dead decorators pointed at deleted interfaces are not a safe intermediate state if you don't also delete the wiring | Deleted the wiring with the call site; kept the pattern to reapply when a new call site needs it ([0035](architecture/0035-password-grant-and-bearer-auth-are-removed.md), [0041](architecture/0041-circuit-breakers-guard-the-remaining-openemr-calls.md)) |
| Fix multi-instance support now vs. defer | Now: no future surprise when scaling | Now: speculative work against a requirement that doesn't exist yet | Deferred explicitly, with the exact preconditions for revisiting written down ([0028](architecture/0028-browser-login-and-session-assume-a-single-instance.md)) |

---

## 7. The accepted-risk ledger (a technique worth copying)

The single most reusable practice found in this project's ADRs: **a capstone
document that lists what was deliberately not fixed, next to what was**, so a
risk is either addressed or *consciously* owned — never just forgotten by
omission. See [0047](architecture/0047-the-identity-bridge-design.md),
Consequences → Accepted risks, for the live example: no legacy-side token
revocation, multi-instance untested, authorization not built, allowlist-only
admin revocation, scope not widened for unneeded API access, partial seed
registration, an upstream defect with no regression guard.

For any legacy-auth migration, maintain this ledger from day one and revisit
it at every milestone gate:

- What did we decide *not* to build, and why was that the right call *then*?
- What would have to be true for that decision to need revisiting?
- Who/what inherits this risk downstream (a new service, a new client type)?

---

## 8. Migration checklist

Use this phase-gated; don't treat it as a flat list to complete in order —
some items only become relevant at a later phase.

**Phase 0 — before writing code**
- [ ] Answered every question in §1 with the actual stakeholder, in writing.
- [ ] Confirmed whether the legacy system exposes a standard protocol or only
      a proprietary login path.
- [ ] Decided: delegate credential verification, or build your own store?
      (Default: delegate.)
- [ ] Named any temporary stand-in explicitly, with its deletion criterion
      written down before it's built.

**Phase 1 — stand-in / unblock**
- [ ] Credentials, if they must pass through your component at all, are
      never logged and are cleared after single use.
- [ ] Integration tests cover the real legacy system, not a mock of it, for
      at least the credential-verification path.
- [ ] Scopes/claims requested are minimal, not "everything, to be safe."

**Phase 2 — real browser flow**
- [ ] Authorization-code (+ PKCE for public clients) replaces any
      password-in-the-client flow.
- [ ] The browser never receives the legacy system's own token in any
      response body.
- [ ] Session is established via a cookie; TLS, Secure, HttpOnly, SameSite
      are all actually true end to end (not "true in code but TLS isn't
      terminated yet").

**Phase 3 — delete the stand-in**
- [ ] Confirmed zero production callers of the stand-in path.
- [ ] Deleted the stand-in's code, tests, and any now-empty layers/projects
      — not left as dead scaffolding.
- [ ] Regenerated any committed API snapshot / client that referenced it.

**Phase 4 — harden**
- [ ] Rate limiting on every unauthenticated entry point.
- [ ] Circuit breakers on every outbound call to the legacy system.
- [ ] Session state is revocable server-side (self-service and admin paths).
- [ ] Logout ends the session on both sides, using the legacy system's real
      logout contract (verified against source/discovery, not assumed).
- [ ] Encryption keys, signing certs, and any state-token store survive a
      process restart.
- [ ] Audit log covers login outcomes and any proxied data access, with zero
      credentials/tokens in it.
- [ ] Start-up ordering: refuse traffic until dependencies are confirmed
      ready.

**Phase 5 — scale**
- [ ] Audited every "per-process" artifact (temp files, in-memory key rings,
      ephemeral certs) before running more than one instance.
- [ ] Shared backing store + shared key ring in place, or session affinity is
      an explicit, written-down interim measure (not a silent assumption).
- [ ] Verified live: restart mid-session, restart mid-login, confirm nothing
      breaks.

**Phase 6 — write the capstone**
- [ ] One document states the finished design's guarantees end to end.
- [ ] One section lists accepted risks explicitly, each with why it's
      accepted and what would change that.
- [ ] Downstream teams/services know they inherit those risks until they
      personally need the gap closed.

**Ongoing — every new service added behind the bridge**
- [ ] Token validation happens at the edge/gateway, not reinvented per
      service.
- [ ] Service-to-service auth has its own decision, not a reuse of the
      browser session mechanism.
- [ ] Authorization (what, not just who) is designed per service; it is not
      inherited from the identity bridge.

---

## 9. Setup, configuration & operational decision points (from the code)

The ADRs capture *decisions*. The running code under
[`src/Services/AuthFacade`](../src/Services/AuthFacade) and the root
[`docker-compose.yml`](../docker-compose.yml) also encode a second layer of
smaller, framework-agnostic setup/configuration choices that never got their
own ADR but recur in almost any legacy-auth migration. These are
implementation-pattern-level, not OpenEMR- or .NET-specific — restate them in
whatever stack you're using.

### 9.1 Configuration and options

- **Fail fast on real misconfiguration; don't fail fast on a deliberately
  conservative empty default.** Required connection settings are bound and
  `ValidateDataAnnotations().ValidateOnStart()`ed so a bad deploy crashes at
  startup, not on the first request
  (`OpenEmrNext.AuthFacade.Composition/OpenEmrOptions.cs`). The admin
  session-revocation allowlist is bound with **no** such validation, because
  an empty list is a valid, safe default (nobody can revoke anyone — fails
  closed), not a misconfiguration
  (`OpenEmrNext.AuthFacade.Composition/AdminOptions.cs`,
  `CompositionServiceCollectionExtensions.cs`). The general rule: decide,
  per setting, whether "absent" means "broken" or "safe," and validate
  accordingly — don't apply one blanket policy to every option class.
- **Let the absence of a config section itself select dev vs. real
  infrastructure**, instead of a separate feature flag or build
  configuration. `AuthFacadeDbOptions.IsConfigured` and
  `AuthFacadeCertificateOptions.IsConfigured` are `true` only when every
  field needed for the real backend (MariaDB, persisted certs) is present;
  when false, the code falls back to an ephemeral per-process store (SQLite,
  OpenIddict's dev certificates) — the same behavior the project had before
  the hardening ADRs, still what every unit test and a bare `dotnet run`
  outside Docker uses today. One codebase, two environments, selected by
  what configuration is actually present rather than by an explicit
  `IsDevelopment()` branch scattered through the feature code.
- **Enforce required secrets at the orchestration layer, not just in app
  code.** Compose's `${VAR:?Set VAR in .env}` syntax refuses to start the
  stack at all if a required secret is unset, with a message naming exactly
  what to set (`docker-compose.yml`) — cheaper to diagnose than a container
  that starts, binds options, and only then throws deep in a stack trace.
- **Default every environment-sensitivity switch to the conservative
  setting**, with explicit opt-in to anything looser: `ASPNETCORE_ENVIRONMENT`
  defaults to `Production` for every service (`docker-compose.yml`), and
  dev-only surface (the OpenAPI document, the Scalar UI) is gated behind
  `app.Environment.IsDevelopment()` in code
  (`OpenEmrNext.AuthFacade.Api/Program.cs`) — so a misconfigured or
  forgotten environment variable fails toward *less* exposure, not more.

### 9.2 Secrets and certificates

- **Size secret protection to the actual threat model, not uniformly.** The
  OpenIddict encryption/signing certificates are persisted with an empty
  password — acceptable because they protect only local, at-rest
  confidentiality on one machine, the same call already made for the
  gateway's own TLS certificate (`docker-compose.yml`'s
  `AuthFacadeCertificates__Password: ""`, cross-referenced to ADR 0039). A
  secret crossing an actual trust boundary (a database password, a client
  secret) is still required and still enforced at orchestration time (9.1).
  Don't reflexively encrypt-with-passphrase everything; decide per artifact
  what it actually protects against.
- **Anything that must survive a restart cannot live only in process
  memory or a per-process temp file.** Before hardening, the OpenIddict
  state-token store was a process-unique SQLite temp file and its certs were
  regenerated fresh every start — both silently broken by a restart. Audit
  every "generated fresh at startup" artifact in a legacy-auth bridge for
  this exact failure mode before calling the design restart-safe.
- **Generate secrets and dev certificates with scripts, never commit
  them.** `scripts/setup/new-env.sh`, `new-dev-cert.sh`, and
  `new-authfacade-certs.sh` produce the `.env` file and certificate material
  locally; none of it is checked in. A migration's setup docs should name
  the exact script/command that produces each secret, not just "set this
  env var."

### 9.3 Startup and bootstrap sequencing

- **Readiness has two layers, and they fail differently on purpose.** The
  orchestrator's own health check (`depends_on: condition: service_healthy`)
  covers the common case. The app's *own* internal poll of the same
  readiness endpoint is a second line of defense for when it runs outside
  the orchestrator (a bare `dotnet run`, a race) — and it is deliberately
  **fail-open**: if the dependency never reports ready within the deadline,
  it logs a warning and starts anyway, because "a request that then fails
  fails no worse than it would have without the check, and a host that never
  starts is worse than one that started slow"
  (`OpenEmrNext.AuthFacade.Api/Startup/OpenEmrReadinessGate.cs`). Contrast
  this with bootstrap/seed scripts (next bullet), which are deliberately
  **fail-closed**.
- **Bootstrap/provisioning is its own idempotent, one-shot step — never
  folded into app startup, and never assumed to have run.** The OAuth client
  registration and the dedicated database creation are separate one-shot
  Compose services (`seed`, `seed-authfacade-db`) that upsert rather than
  blindly insert — "safe to run again: it adds the client only if missing and
  refreshes its scope, redirect URI, secret and logout redirect URI"
  (`scripts/seeds/authfacade-client.php`). They poll their own dependency
  (OpenEMR's schema existing) with a bounded retry count and **exit
  non-zero** on timeout — the opposite failure mode from the app's own
  readiness gate, because nothing should proceed on half-finished
  provisioning. The app container then declares a hard dependency
  (`condition: service_completed_successfully`) so starting it always pulls
  the seed in too, closing the gap where changing a registered value (a
  redirect URI) silently left a stale registration in place until someone
  remembered to rerun the seed by hand (`docker-compose.yml`, the comment on
  `auth-facade`'s `depends_on`).

### 9.4 Request pipeline and middleware ordering

- **Order security middleware deliberately and write down why, because the
  order is itself a security property.** Forwarded-headers resolution runs
  before the rate limiter and authentication, since both depend on seeing
  the request's real scheme and client address, not the proxy hop's own
  (`OpenEmrNext.AuthFacade.Api/AuthFacadeApiExtensions.cs`). Rate limiting
  runs before authentication, so an over-budget request is rejected before
  any handler — or any call to the legacy system — runs at all.
- **Verify a security-critical framework feature actually behaves as
  documented on your runtime before depending on it; hand-roll the minimal
  check if it doesn't.** ASP.NET Core's built-in `ForwardedHeadersOptions`
  did not actually restrict anything against the installed runtime in
  testing, confirmed in an isolated reproduction outside the codebase — so a
  small, directly auditable middleware replaced it instead of depending on
  that behavior being fixed later
  (`OpenEmrNext.AuthFacade.Api/ForwardedHeaders/ForwardedHeadersServiceCollectionExtensions.cs`).
  This generalizes past forwarded headers: trust nothing security-critical
  from a library without a reproduction, in any stack.
- **A multi-step flow gets one shared rate-limit budget, not one per
  endpoint.** `/oidc/login` and `/callback` are two requests but one logical
  attempt; splitting the budget across them would silently double it
  (`OpenEmrNext.AuthFacade.Api/RateLimiting/RateLimitingServiceCollectionExtensions.cs`).
- **Decide queue vs. reject-fast for rate-limited auth endpoints
  explicitly.** `QueueLimit = 0` rejects immediately over budget rather than
  queueing — appropriate for a login flow, where queueing a flood of
  attempts just delays the same outcome; a different endpoint class might
  reasonably choose to queue instead. Make the choice, don't inherit the
  library default silently.

### 9.5 Resilience primitives

- **A circuit breaker must be a long-lived, shared instance per guarded
  dependency — never constructed fresh per call.** Its whole value is state
  that spans requests ("a pipeline to share across requests, so that its
  state spans them,"
  `OpenEmrNext.AuthFacade.Api/Resilience/CircuitBreakerPipeline.cs`); a
  breaker rebuilt on every call can never actually trip. This is the single
  easiest way to implement a circuit breaker that does nothing.

### 9.6 Network topology and addressing

- **Pin the internal network topology when a security control depends on
  recognizing it.** The Compose network uses a fixed subnet, not the
  orchestrator's dynamically assigned default, purely so the
  forwarded-headers trust check has a stable CIDR to check against
  (`docker-compose.yml`'s top-level `networks` block, ADR 0039).
- **Bind every exposed port to loopback by default; require explicit
  opt-in to bind wider.** Every service's published port uses
  `${HOST_BIND_ADDRESS:-127.0.0.1}` (`docker-compose.yml`) — a reusable
  default for any local or single-host deployment of a legacy-auth bridge.
- **Every address-shaped config value needs an explicit answer to "internal
  or external?", and this bug class recurs.** `BaseUrl` (service-to-service),
  `Issuer` (must match what's inside the token, which may differ from
  `BaseUrl`), and `RedirectUri`/post-logout URI (browser-facing, must go
  through whatever the browser can actually reach, e.g. a gateway) were each
  hit as a *separate* internal/external mismatch across different ADRs
  (0012, 0026, 0034) in this project. When auditing a legacy-auth bridge's
  configuration, classify every URL-shaped setting as one or the other
  explicitly; don't assume "the base URL" is single-valued.

### 9.7 Cross-service configuration agreement

- **When two independently deployed units must agree on a value and there
  is no shared-config mechanism, duplicate the computation explicitly and
  say why** rather than building a shared-config system prematurely. The
  seed service and the AuthFacade container each compute the same redirect
  URI formula independently, because "Compose does not share one service's
  environment values with another" (`docker-compose.yml`'s comments on
  `AUTHFACADE_REDIRECT_URI` and `OpenEmr__RedirectUri`). This is a
  deliberate, documented duplication — not a DRY violation to "fix" by
  introducing shared config ahead of an actual need.
- **Know and document the far side's exact-match semantics before assuming
  "close enough."** OpenEMR compares the post-logout redirect URI with a
  plain SQL `=`, not a host allow-list (`scripts/seeds/authfacade-client.php`,
  ADR 0040) — a trailing slash or a different scheme breaks it silently
  with no error surfaced to the user. Any value two systems must agree on
  needs this question answered explicitly: string equality, host match, or
  allow-list prefix?

### 9.8 Build and deploy topology

- **A service's container build context must extend to cover every shared
  code reference, even across directory boundaries.** `webapp`'s build
  context is the repository root, not its own subdirectory, because it
  references a client project that lives under a different service's
  directory (`docker-compose.yml`, the comment on `webapp.build.context`).
  A monorepo with generated/shared clients (here: an NSwag-generated client,
  ADR 0031) hits this as soon as the consuming service is containerized
  separately from the one that owns the API it calls.

### 9.9 Containerization

- **Multi-stage builds: a full SDK to compile, a minimal runtime to ship.**
  Every Dockerfile builds with the full `dotnet/sdk` image and ships only the
  `dotnet/aspnet` runtime image (`src/Services/AuthFacade/Dockerfile`,
  `.../Gateway/Dockerfile`); WebApp goes further, since a compiled Blazor
  WebAssembly app is just static files — its runtime stage is `nginx:alpine`
  with no .NET runtime at all (`src/Services/WebApp/Dockerfile`). Ship the
  smallest image that can actually run the artifact, not the image that built
  it.
- **Order `COPY`/`RUN` for layer-cache reuse: project files and `restore`
  before the rest of the source.** Every Dockerfile copies only the
  `.csproj` files and runs `dotnet restore` before copying the remaining
  source and publishing — a source change invalidates only the publish
  layer, not the (slow) dependency-restore layer. Generalizes past .NET:
  copy manifest/lockfiles (`package.json`+lockfile, `go.mod`, `requirements.txt`)
  and install dependencies before copying application source, in any
  language.
- **Run as the image's non-root user by default; know why before
  overriding it.** Every service runs as the base image's non-root `app`
  user (`USER $APP_UID`) rather than root, with the reasoning stated
  inline — "port 8080 needs no privileges" — and generalized project-wide in
  [ADR 0020](architecture/0020-local-stack-defaults-are-conservative.md). A
  legacy-auth bridge is exactly the kind of component where a container
  escape matters most; don't let convenience during development (root is
  sometimes easier to debug with) become the shipped default.
- **`EXPOSE` documents intent; it does not create a listener.** The gateway
  `Dockerfile` declares `EXPOSE 8443` for HTTPS with a comment noting the
  port "listens on nothing" until `docker-compose.yml`'s `ASPNETCORE_URLS`
  override actually binds it — a reminder that `EXPOSE` is metadata, and the
  real bind configuration (env var, code, or orchestration layer) is what
  matters operationally.
- **Pin runtime/base images by digest, not just by tag, and say so when you
  deliberately don't.** `.env.example` pins `MARIADB_IMAGE` and
  `PHPMYADMIN_IMAGE` by digest ("so a rebuild cannot change them silently")
  but pins `OPENEMR_IMAGE` by tag only, with the reason stated explicitly —
  the committed database snapshot was produced against that exact version,
  so the tag itself is the compatibility constraint, not a convenience.
  Every deviation from "pin by digest" should be this explicit, not silent.
- **Mount read-only wherever the container only needs to read.** Certificates
  and seed scripts are bind-mounted `:ro` (`docker-compose.yml`'s
  `./certs:/certs:ro`, `./scripts/seeds/...:ro`) — a container compromised
  through the app it runs cannot use that foothold to modify the secret
  material or the provisioning script that produced its own configuration.
- **Choose `restart` policy per container role, not uniformly.** Long-running
  services use `restart: unless-stopped`; one-shot provisioning containers
  (`seed`, `seed-authfacade-db`) use `restart: "no"` — a seed container that
  restarted automatically on exit-0 would either loop forever or mask a
  real failure as a crash-restart.
- **State that local-stack credentials are deliberately known/shared, if
  they are, rather than letting that fact hide in `.env.example`.**
  [ADR 0020](architecture/0020-local-stack-defaults-are-conservative.md)
  says outright that "the stack's credentials... are known and shared with
  CI," and compensates with network defaults (loopback binding) rather than
  secrecy — the right call for a disposable local/CI stack, and a decision
  that would need to flip (real secrets, real rotation) the moment this
  stack stops being disposable.
- **A healthcheck should probe actual readiness, not just that the process
  started** — covered in 9.3, but worth repeating here as a Dockerfile/
  Compose-level concern: a `CMD` healthcheck hitting a real readiness
  endpoint (`docker-compose.yml`'s `openemr` healthcheck) catches the
  "container running, application not yet serving" gap a bare port-open
  check or `restart: unless-stopped` alone would miss entirely.

### 9.10 Data lifecycle

- **Decide the schema-creation strategy deliberately, and know when to
  graduate off it.** The bridge's own session-state database is created
  with `EnsureCreated` at app startup
  (`OpenEmrNext.AuthFacade.Api/Program.cs`), appropriate while the schema is
  small and still moving; this is a conscious simplification to revisit with
  versioned migrations before anything resembling a production rollout,
  not an oversight.

## 10. Testing strategy

Derived from `tests/Services/AuthFacade/` — three distinct test projects,
each proving something the others structurally cannot. Treat this as the
test-layer shape for *any* legacy-auth bridge, not an AuthFacade-specific
detail.

### 10.1 The layers, and what each one alone can prove

| Layer | What it checks | What it cannot catch | Evidence |
|---|---|---|---|
| Architecture tests | The compiled reference graph matches the intended layering (e.g., the config-binding layer never references the web framework) | Anything about runtime behavior — this is a structure check, not a behavior check | [`LayerDependencyTests.cs`](../tests/Services/AuthFacade/OpenEmrNext.AuthFacade.ArchitectureTests/LayerDependencyTests.cs), [ADR 0007](architecture/0007-layered-architecture-with-enforced-dependencies.md) |
| Unit/in-process tests | Protocol-level correctness (token exchange, signature/nonce/issuer validation, session persistence, resilience thresholds, redaction) against a real-but-fake legacy IdP, in-process, fast and parallelizable | Anything about the *actual* legacy system's own behavior — only ever proves the code handles what the fake server sends | `tests/.../OpenEmrNext.AuthFacade.UnitTests/Api/*` |
| Black-box integration tests | The real legacy system, with the real seed-registered client, actually accepts what the bridge sends — redirect URI, client secret, discovery document shape | Only what's exercised; this project's own suite explicitly does *not* drive the full login flow, only the pieces around it | [`AuthFacadeStackTests.cs`](../tests/Services/AuthFacade/OpenEmrNext.AuthFacade.IntegrationTests/AuthFacadeStackTests.cs), [ADR 0006](architecture/0006-black-box-integration-tests.md) |
| Shadow comparison (planned, not yet built) | Legacy and new code paths produce equivalent outcomes for the same scenario, before cutover | Nothing by itself — it's a pre-cutover gate, not a substitute for the layers above | ROADMAP.md, M5 |

A rule that falls out of this table: **an architectural rule you can state in
one sentence should be an automated test, not a comment or a code-review
checklist item.** `LayerDependencyTests` inspects the *compiled assembly's*
reference list, so a forbidden reference fails the build even before
anything calls it — catching the violation at the moment it's introduced,
not whenever someone happens to exercise that path.

### 10.2 Testing the legacy-system integration itself

- **Stand up a real, minimal, protocol-speaking fake server for the legacy
  IdP; don't mock the HTTP client.** `FakeOpenEmrServer` is an actual
  ASP.NET Core app on a random loopback port, implementing just enough of
  OpenEMR's token, JWKS, and a protected API route, driven through the
  *real* OpenIddict.Client library end-to-end
  (`tests/.../FakeOpenEmrServer.cs`). This catches protocol-shape bugs a
  mocked `HttpClient` would hide entirely — building this fake surfaced,
  for example, that OpenIddict rejects an `id_token` with no `iat` claim, a
  requirement a mock would never have been made to enforce. When the thing
  under test is protocol compliance, fake the protocol; don't mock your own
  client's interface to it.
- **Prove the legacy system accepts your registration by asking the legacy
  system directly, not just by asserting your own config.** The integration
  suite sends a real authorize request and a real (deliberately invalid)
  token request straight to OpenEMR with the seeded client's credentials,
  checking OpenEMR's own response rather than reading back the
  `oauth_clients` row and trusting it matches
  (`AuthFacadeStackTests.OpenEmr_accepts_the_AuthFacade_redirect_uri`,
  `...authenticates_the_AuthFacade_client_secret`). A redirect URI or secret
  that drifted out of sync (e.g., after the ADR 0033 gateway move) fails
  here with `invalid_client`, before any human notices through a real
  browser.
- **For every resilience threshold, test the negative space as explicitly
  as the positive case.** `CircuitBreakerTests` doesn't just prove repeated
  5xxs open the circuit — it has a separate test proving a 4xx (a normal
  rejected grant) *never* contributes toward opening it, however many times
  it happens, and another proving a 404 from a proxied call doesn't trip
  the proxy's breaker either. It is easy to write "the breaker opens" and
  never write "...and doesn't open for the things that shouldn't count,"
  and that second test is the one that actually catches a wrong `ShouldHandle`
  predicate.
- **Make slow-by-design thresholds fast in tests, through the same
  configuration surface production uses — not a separate test-only code
  path.** Circuit-breaker and rate-limit windows are overridden to a
  handful of calls/seconds in tests via the same options binding production
  reads (`CircuitBreakerTests.LowThresholds`), so the test exercises the
  real decision logic, just on a compressed timeline, rather than a
  hand-rolled shortcut that bypasses it.

### 10.3 Testing security-sensitive guarantees

- **Test a "never happens" claim at the most permissive configuration the
  system could ever run at, and guard against a vacuous pass.**
  `RequestLoggingTests` asserts a marker planted in a proxied request body
  never appears in *any* captured log entry, with logging opened all the
  way to `Trace` — not the default level — since that's the permissive
  extreme a misconfigured deployment could actually reach. It then asserts
  the capture itself produced *some* log output before asserting the
  marker's absence (`Assert.NotEmpty(logs.Entries)` before
  `Assert.DoesNotContain`), specifically so the test can't silently pass
  because its own instrumentation broke and there was nothing to search.
  Any "X must never appear" test needs that same companion assertion, or it
  can pass for the wrong reason forever.
- **When a setting is optional-with-a-safe-fallback, test both branches
  with real artifacts, not just that each one binds.** `PersistedCertificateTests`
  generates real, temporary self-signed PKCS#12 files — the same shape the
  real setup script produces — and drives a full login round trip against
  them, specifically because every *other* test leaves that config unset
  and only ever exercises the fallback branch. A config-driven fork (§9.1)
  is two code paths; test both, not just the one every other test happens
  to hit by default.

### 10.4 Integration-test readiness fixtures

- **Poll every independent readiness condition concurrently, under one
  shared deadline, with its own per-check timeout** — not a single serial
  chain of waits. `ReadinessFixture` checks the database, the seed's row,
  OpenEMR's probe, and AuthFacade's own health endpoint all at once each
  round, so one slow check doesn't starve the deadline budget for the
  others (`tests/.../ReadinessFixture.cs`).
- **Distinguish "this will never pass, stop polling" from "not ready yet,
  keep polling," and fail loudly and specifically on the former.** A MySQL
  access-denied error short-circuits immediately with a message pointing at
  the likely cause (`DB_PASSWORD` mismatch against an existing volume) —
  the same fail-fast-vs-fail-open distinction as §9.3's startup gates,
  applied to the test harness this time instead of the app itself.
- **Serialize tests that share one expensive, stateful external resource;
  keep everything else parallel.** Integration tests against the live
  Compose stack share one xUnit collection and run one after another, with
  one readiness wait up front; unit tests against `FakeOpenEmrServer`
  start an isolated in-process instance per test and run freely in
  parallel. Don't let a shared live dependency force the whole suite
  serial — only the tests that actually touch it.

### 10.5 Test-scope discipline

- **State each test layer's actual boundary where a future reader will see
  it**, not just in a design doc. `AuthFacadeStackTests`'s own class comment
  says outright that it "does not exercise the login flow" — so nobody
  reads a green CI run on that suite as proof the login flow works.
- **When a feature is removed, decide what happens to its test coverage
  explicitly — don't let it dangle.** Removing the password grant
  (ADR 0035) deleted `MeEndpointTests`/`TokenEndpointTests` outright rather
  than leaving them disabled, and the ADR says plainly what coverage that
  leaves missing (no live, non-unit-test coverage of the cookie-based `/me`
  against the real stack) rather than letting the gap go unstated. A
  shrinking test suite should be a decision with a stated consequence, the
  same discipline as the accepted-risk ledger (§7).

## 11. Production readiness checklist

The migration checklist (§8) gates *building* the bridge. This one gates
*exposing it to real users and real data* — a separate, later bar. Treat
every unchecked item as an explicit accepted risk (§7), not a silent gap,
until it's checked.

**This project's own status, stated plainly:** nothing in this repository is
approved for production clinical use today — [`SECURITY.md`](../SECURITY.md)
and [`docs/environments.md`](environments.md) both say so outright, and
`docs/environments.md` defines only three environments (Development, Test,
Sanitized integration), deliberately stopping short of a fourth,
production tier. Several items below are exactly *why*: they're accepted
risks today ([ADR 0047](architecture/0047-the-identity-bridge-design.md),
[ADR 0020](architecture/0020-local-stack-defaults-are-conservative.md)), not
oversights, and they're the actual gate between this bridge's current state
and a real production deployment — for this project or for anyone else's
AuthFacade-shaped bridge.

**Secrets and key management**
- [ ] Every secret (DB passwords, OAuth client secret, cert passphrases)
      comes from a real secrets manager with access control and rotation —
      not a `.env` file with "known and shared with CI" credentials
      ([ADR 0020](architecture/0020-local-stack-defaults-are-conservative.md)).
- [ ] Encryption/signing certificates (OpenIddict's, the gateway's TLS cert)
      are CA-issued or otherwise trust-chain-valid, not self-signed dev
      certs from a local setup script, and have a documented rotation
      procedure with no-downtime rollover.
- [ ] The Data Protection key ring and any session/state-token store have a
      key-rotation plan that doesn't invalidate every live session at once.
- [ ] Per-environment separation matches [`docs/environments.md`](environments.md)'s
      rule: distinct identity-provider config, database/storage credentials,
      encryption/signing keys, and observability destinations, with no
      lower-trust environment able to reach a higher-trust one.

**Scaling and availability**
- [ ] Multi-instance operation is *proven*, not just unblocked. Closing the
      structural blockers (shared key ring, persisted certs, shared
      state-token store) is a precondition, not the same claim as having
      actually run two replicas under load and through a rolling deploy
      ([ADR 0028](architecture/0028-browser-login-and-session-assume-a-single-instance.md),
      Consequences — this exact gap, unresolved at time of writing).
- [ ] A rolling deploy mid-login and mid-session has been tested, not just
      a clean restart.
- [ ] Resource limits (CPU/memory) and autoscaling or capacity targets are
      set from an actual load test, not left at orchestrator defaults.
- [ ] Rate-limiter keying has been re-validated against the real production
      network path. A limiter keyed on remote address works differently
      once there's more than one hop or load balancer in front of it than
      it does behind a single local gateway.

**Resilience and dependency on the legacy system**
- [ ] Every known upstream legacy-system defect affecting auth (this
      project's example: OpenEMR returning 500 instead of a clean rejection
      on some wrong-password attempts) is either fixed upstream, has a
      compensating control, or is a formally signed-off accepted risk — not
      silently unguarded, which is where this project currently leaves it
      ([ADR 0047](architecture/0047-the-identity-bridge-design.md)).
- [ ] Circuit breakers, rate limits, and timeouts are tuned from real
      traffic/latency data, not the conservative defaults chosen for a local
      stack with no real load.
- [ ] A degraded-legacy-system runbook exists: what a human does when the
      circuit breaker trips, who gets paged, what users see.

**Authorization**
- [ ] A real authorization model exists for what an authenticated identity
      may do with legacy-system (and any new service's) data — "the bridge
      confirms identity only" is an accepted risk at prototype stage, not a
      production posture
      ([ADR 0047](architecture/0047-the-identity-bridge-design.md),
      Accepted risks).
- [ ] Admin-level actions (session revocation, impersonation, support
      tooling) are gated by a real role/claim, not a fixed config-file
      allowlist ([ADR 0045](architecture/0045-silent-refresh-and-admin-session-revocation.md)).
- [ ] OAuth/OIDC scopes requested are exactly what's needed for what's
      actually built — widen deliberately per real caller, never
      preemptively.

**Observability and incident response**
- [ ] Metrics and distributed tracing exist across the bridge and the
      legacy system boundary, not just the audit log — this project's own
      roadmap defers both explicitly ("Metrics and tracing come later,"
      `ROADMAP.md`).
- [ ] Audit log coverage, retention, and redaction match
      [`docs/environments.md`](environments.md)'s rule ("logs must redact
      tokens, credentials, personal data, and clinical content by default")
      and your actual compliance regime, not just "login outcomes and the
      BFF proxy" ([ADR 0042](architecture/0042-audit-log-scoped-to-login-and-the-bff-proxy.md)).
- [ ] Alerting exists for the security-relevant signals that matter in
      production: spikes in failed logins, circuit-breaker trips, rate-limit
      rejections, and session-revocation calls.
- [ ] An incident runbook exists for a suspected compromised session or
      credential: who can invoke `/sessions/revoke/{subject}` (or your
      equivalent), how fast, and what gets checked afterward.
- [ ] Backup and restore is tested for whatever now holds session/identity
      state (the dedicated session database here), including a timed
      restore-from-backup drill, not just a volume that happens to persist.

**Supply chain and build**
- [ ] Every base/runtime image is pinned by digest, not tag, with a
      documented, deliberate exception policy — matching this project's own
      stated exception for `OPENEMR_IMAGE` (`.env.example`), not silently
      drifting to "pin everything except what's inconvenient."
      Dependency/image scanning runs in CI, not just at build time.
- [ ] Schema management has moved from `EnsureCreated` (acceptable while the
      schema is small and moving) to versioned, reviewed migrations with a
      tested rollback path (§9.10).

**Compliance and sign-off**
- [ ] Whatever regulatory framework governs the data this system will
      touch (HIPAA, for health data in this project's domain; GDPR, PCI-DSS,
      or others elsewhere) has been reviewed against the actual design, not
      assumed satisfied because credentials are delegated to a legacy
      system that was itself compliant.
- [ ] A security review/penetration test has been performed against the
      production configuration specifically — a review of the local/dev
      stack (such as [ADR 0047](architecture/0047-the-identity-bridge-design.md)'s
      own design review) is necessary but not sufficient; production adds
      real network exposure, real secrets, and real data.
- [ ] The vulnerability-disclosure and triage process
      ([`SECURITY.md`](../SECURITY.md)'s acknowledgment/severity-assessment
      targets, here) is live and staffed before production traffic exists,
      not written and left dormant.
- [ ] A rollback plan exists: if the bridge must be pulled from production,
      what happens to active sessions, in-flight logins, and the legacy
      system's own OAuth client registration.

## 12. Security testing

§10 covers test *layers*; this is the attacker's-eye cut through the same
suite — the tests that exist specifically because someone asked "what if a
caller lies here?" Each pattern below is a category to apply to any
legacy-auth bridge, not just the specific endpoint it was found guarding in
this one.

### 12.1 Forgery and replay of the identity assertion

- **Reject a correctly signed, correctly issued, correctly audienced token
  that fails on the one remaining check — and test that specific
  combination, not just "a bad token."** `Callback_with_a_mismatched_nonce_redirects_with_login_failed`
  uses a genuinely valid `id_token` (real signature, right issuer, right
  audience, signed by the real fake-IdP key) with only the `nonce` wrong,
  specifically because a token that's invalid in *every* dimension at once
  would never prove the nonce check is the thing doing the rejecting
  (`OpenIddictClientCallbackTests.cs`). This generalizes directly: for every
  independent validation check (issuer, audience, signature, nonce,
  expiry, `aud`), the test that matters most is "everything else valid, only
  this one wrong" — it's the only version that proves *that* check, instead
  of just proving *some* check exists.
- **Prove a rejected grant degrades to a generic error, not the dependency's
  own error text.** `Callback_whose_grant_openemr_rejects_redirects_with_login_failed`
  confirms a rejected OpenEMR grant surfaces as the bridge's own
  `?error=login_failed`, never OpenEMR's own response body — relevant for
  information disclosure (an attacker probing for which part of a login
  attempt failed) as much as for UX.

### 12.2 Trust-boundary and spoofing tests

- **Test the trust boundary from both sides, not just the "it works" side.**
  `ForwardedHeadersTests` pairs `A_forwarded_https_header_from_the_trusted_network_is_honored`
  with `A_forwarded_https_header_outside_the_trusted_network_is_ignored` —
  the same header, the same claimed value, with only the caller's network
  membership changed. A suite that only tests the trusted path can't
  distinguish "the trust check works" from "the header is always honored."
  It also tests the un-forwarded default (`No_forwarded_header_means_an_ordinary_http_request`)
  and a downstream consequence of the trust decision
  (`The_openiddict_correlation_cookie_is_secure_behind_the_trusted_proxy`) —
  proving the spoofing defense actually changes security-relevant behavior
  (a cookie's `Secure` flag), not just an internal flag nothing reads.
- **For any security-relevant middleware driven by configuration (a trusted
  CIDR, an allowed-host list, a signing key path), test that bad
  configuration fails the host at startup — never falls back to an open or
  permissive default.** `Host_refuses_to_start_with_an_invalid_cidr` and
  `..._with_a_missing_cidr` prove an unparseable or absent trusted network
  setting crashes the host rather than silently trusting every caller. The
  generalizable test shape: *invalid* security config is a startup test
  with an expected exception, exactly as much as *valid* config is a
  behavior test.

### 12.3 Session security and privilege-boundary tests

- **Prove revocation kills the session server-side, from a cookie copy the
  revoking call never touched.** `SessionRevocationTests` logs in as an
  independent "browser" (its own cookie jar), revokes from that same
  browser, then re-asserts against it — proving the record died, not just
  that the call that issued the revoke stopped working. This matters
  because the threat model for revocation is specifically "a copy of this
  cookie exists somewhere I don't control" — a test that only checks the
  revoking caller's own next request can't distinguish real server-side
  revocation from a client-side no-op.
- **Test blast radius in both directions for any "affects multiple
  sessions" action: it must reach every session it should, and none it
  shouldn't.** `Revoking_all_sessions_ends_every_session_for_the_same_subject_not_just_the_caller`
  and `Revoking_all_sessions_does_not_affect_a_different_subjects_session`
  are a matched pair — the first guards under-reach (an attacker's second
  session surviving a "revoke all" the legitimate user triggered), the
  second guards over-reach (one subject's action silently affecting
  another's). Write both halves of this pair for every bulk action a
  legacy-auth bridge exposes.
- **Test the negative case of a privilege boundary as thoroughly as the
  positive case, and assert the attempt changed nothing.**
  `An_allowlisted_admin_subject_can_revoke_another_subjects_sessions` and
  `A_subject_not_on_the_admin_allowlist_gets_forbidden_and_changes_nothing`
  are the same action attempted by an authorized and an unauthorized
  caller; the second doesn't stop at asserting `403` — it re-checks that
  the target's session is still alive, proving the rejected attempt had
  zero side effects, not just a refused response wrapped around a side
  effect that happened anyway.
- **Test the self-targeting edge case of any admin/elevated action
  separately.** `An_admin_revoking_their_own_subject_is_signed_out_too`
  exists because "can affect anyone" and "can affect anyone, including
  themselves" are easy to get different answers to by accident (e.g., a
  caller-exclusion check added for a different reason).
- **Every session-mutating or identity-revealing endpoint needs its own
  "no session, no access" test**, not an inherited assumption from one
  endpoint's coverage. `Revoke_all_requires_an_authenticated_session` and
  `Admin_revoke_requires_an_authenticated_session` are separate tests for
  separate endpoints, even though both ultimately hit the same
  authorization middleware — middleware wiring regresses per-route often
  enough (ADR 0035's own history: a policy change once silently stopped
  authenticating the cookie scheme at all) that each sensitive route
  deserves its own assertion.

### 12.4 Abuse and rate-limit boundary tests

- **Test the exact boundary (N passes, N+1 is refused), not "a lot of
  requests get refused eventually."** `Requests_over_the_login_limit_get_429_with_retry_after`
  sends exactly the configured limit, asserts none of them were refused,
  then asserts the very next one is — proving the limit is the configured
  number, not approximately that number.
- **Test that a shared budget across a multi-step flow can't be doubled by
  alternating between its steps.** `Login_and_callback_share_one_budget_per_address`
  interleaves `/oidc/login` and `/callback` calls from the same address and
  confirms the shared budget exhausts at the combined total — the test
  that would catch a regression back to two independent per-endpoint
  limiters (an easy mistake when a second endpoint is added to an existing
  flow).
- **Test that the limiter does *not* apply somewhere it shouldn't, as
  explicitly as proving it applies where it should.**
  `Me_is_not_rate_limited_by_the_login_policy` calls a high-traffic
  authenticated endpoint 15 times (more than the login limit) and asserts
  none are refused — guarding against the opposite bug from 12.4's first
  bullet: a limiter policy accidentally applied too broadly, silently
  breaking a legitimate high-frequency caller.
- **Apply 12.2's "bad config fails startup" rule to rate-limit settings
  too.** `Host_refuses_to_start_with_a_permit_limit_below_one` and
  `..._with_a_window_under_one_second` close off the degenerate
  configurations that would otherwise either lock out every caller or
  rate-limit nothing at all.

### 12.5 Input-boundary tests: a gap worth naming

Not every security-relevant code path in this codebase has a test proving
it, and that's worth stating plainly rather than implying a 100% record.
`ReturnUrlValidator` — the open-redirect guard on `/login`'s `returnUrl`
parameter (§4's hardening table; it resolves anything not on the configured
host allow-list to a safe default) — has **no dedicated test** exercising
its allow-list enforcement or its scheme rejection (`javascript:`, `data:`,
a bare relative path treated as absolute, a host not on the list) at the
time of this audit. This is exactly the kind of gap that's invisible until
someone looks for it: the code is short, looks obviously correct on
inspection, and nothing else in the suite happens to exercise its rejection
branch. **The generalizable lesson is the process, not the specific gap**:
periodically grep for every security-relevant validator/guard in the
codebase and confirm each one has a test that actually drives its rejection
path — "the code is simple" is not evidence that it's tested.

### 12.6 Fail-closed configuration, as a cross-cutting test category

Collecting the pattern that recurs in 12.2 and 12.4: **any option controlling
a security decision (a trusted network, a rate limit, an admin allowlist, a
certificate path) should have a startup test proving invalid or degenerate
values refuse to start the host**, not just tests proving valid values work.
Across this codebase that's `ForwardedHeadersTests`' two CIDR tests and
`RateLimitingTests`' two threshold tests — the same shape, reapplied per
option class. When adding a new security-relevant setting to any bridge,
add its fail-closed test in the same change, not as follow-up.

## 13. Logging and performance monitoring

### 13.1 What's built: structured, stable-category audit logging

- **Give the audit trail its own fixed, stable logger category — independent
  of which file the logging call happens to live in.** `AuditLog` is a
  never-constructed marker type that exists only to pin the category name
  (`OpenEmrNext.AuthFacade.Api.Audit.AuditLog`), so a refactor that moves an
  `AuditEvents` call to a different file doesn't silently rename the
  category a log pipeline is filtering on (`Audit/AuditLog.cs`). Generalize
  past this codebase: decide audit-log category names as a stable contract
  with whatever consumes them (a SIEM query, a log-based alert), and pin
  that name to something refactor-proof, not to the calling type.
- **Use source-generated, strongly typed log methods for anything
  structured and queried, not ad hoc string interpolation.**
  `AuditEvents.LoginSucceeded(logger, subject)` and its siblings are
  `[LoggerMessage]`-generated, giving every audit event a fixed message
  template and named, typed fields (`Subject`, `Reason`, `Method`, `Path`,
  `StatusCode`) that a structured-log backend can filter and aggregate on
  directly, rather than parsing a free-form string.
- **Scope the audit trail to where data actually moves, and say why
  something in scope was deliberately left out.** The audit trail covers
  login outcomes, BFF proxy access, and admin revocation — explicitly *not*
  `/me`, with the reasoning given inline: "`/me` itself returns no PHI, just
  an identity check, and fires on every dashboard load — too high-volume to
  be worth the noise for what it would add" (`Audit/AuditEvents.cs`,
  [ADR 0042](architecture/0042-audit-log-scoped-to-login-and-the-bff-proxy.md)).
  This is a real, generalizable tradeoff: logging volume against a
  per-event signal-to-noise judgment, made explicitly instead of either
  logging everything or logging arbitrarily.
- **Never log a reason string that could leak the dependency's own error
  detail.** `LoginFailed`'s `reason` parameter is documented as "a short,
  fixed internal label... never OpenEMR's own error text or an exception
  message" — the same discipline as 12.1's generic `?error=login_failed`
  response, applied to what reaches the log instead of what reaches the
  browser. A log line is also an exfiltration surface if whoever reads logs
  is a different trust tier than whoever should see the legacy system's
  internal error detail.
- **Redaction is a tested guarantee, not a logging-policy statement** — see
  §10.3's `RequestLoggingTests` coverage (never duplicate that detail here;
  the point is that this section's logging *design* and §10's *test* of it
  are two different artifacts, and a legacy-auth bridge needs both).

### 13.2 Liveness vs. readiness: know which one you actually have

- **`/health` here is pure liveness — a fixed 200, no dependency check at
  all** (`SystemEndpoints.cs`: `Results.Ok(new HelloResponse(...))`, no I/O).
  It answers "is the process up," nothing about whether it can actually
  serve a login (reach the legacy system, reach its own session database).
  Contrast this with OpenEMR's own `/meta/health/readyz`, which the bridge's
  startup gate (§9.3) *does* treat as real readiness. **Know, for every
  health endpoint in the system, which of the two questions it actually
  answers**, and don't let an orchestrator's liveness probe quietly stand in
  for a readiness probe (or vice versa) — a process that's "up" but can't
  reach its session database should fail readiness, not just liveness, or a
  load balancer keeps sending it real traffic it cannot serve.
- A fuller production deployment of this shape of bridge needs a *separate*
  readiness endpoint that actually checks its own dependencies (the session
  database, at minimum) — not present here today, and worth calling out as
  its own gap rather than assuming `/health`'s name implies that coverage.

### 13.3 The named gap: metrics, tracing, and correlation

This project is explicit that it has deferred this category rather than
addressing it: "**Observability:** structured logs, health checks,
correlation across legacy and new components. **Metrics and tracing come
later.**" (`ROADMAP.md`, Cross-cutting workstreams). Treat that sentence as
the honest baseline for *any* early-stage legacy-auth bridge — structured
logs and a liveness check are not the same thing as the observability a real
production deployment needs, and conflating them is exactly how a bridge
ships without knowing it's slow or failing until a user reports it.

What a production-grade version of this bridge's observability adds beyond
what's built:

- **Latency and error-rate metrics per outbound dependency call**, broken
  out individually: the token exchange, the JWKS fetch, and each BFF-proxied
  call — the same three call sites §4's hardening table already names as
  circuit-breaker-guarded. A breaker trips on a *ratio*; you cannot tune
  that ratio, or notice it drifting toward trouble, without the
  latency/error metric underneath it.
- **Circuit-breaker state transitions as a first-class signal**, not just
  the generic failure response a caller sees. Alert on *open* (something
  is actually wrong with the legacy system) and log/metric *half-open*
  probe outcomes — the bridge's own resilience layer is one of the highest
  signal-to-noise indicators of legacy-system health available, and it's
  wasted if only the breaker itself knows it tripped.
- **Rate-limit rejection counts as a security *and* capacity signal**, fed
  somewhere alerting can see it. A spike is either an attack (ties to
  12.4's abuse tests) or a legitimate traffic pattern the configured limit
  no longer fits — the metric is what tells you which, instead of users
  reporting 429s anecdotally.
- **Session-store latency**, once a server-side session store exists
  (§3.2/§4): it sits on the critical path of every authenticated request
  through the bridge, not just login, so its own latency deserves the same
  attention as the legacy system's.
- **Correlation/trace IDs propagated across every hop** (gateway →
  bridge → legacy system, and the reverse for the response), so a slow or
  failing request can actually be followed across the boundary this whole
  framework exists to bridge — otherwise every incident investigation
  starts by manually correlating timestamps across two systems' separately
  formatted logs.

### 13.4 Applying this section

Use §13.1's audit-log pattern (stable category names, structured fields,
explicit in/out-of-scope decisions, redaction discipline) for any new
security-relevant log you add. Use §13.2 to classify every health/probe
endpoint you build as liveness or readiness, explicitly. Treat §13.3 as a
checklist of what must exist before "it has structured logs" is mistaken for
"it is observable in production" — and fold the relevant items into §11's
production readiness checklist for your own migration, the same way this
framework names the gap here rather than leaving it implicit.

## 14. CI/CD

Derived from [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) — one
job that builds the real multi-container stack from a sanitized snapshot and
runs every test layer from §10 against it. The patterns below generalize past
GitHub Actions and past this specific stack.

### 14.1 Test what people actually run — the central principle

- **CI builds its configuration from the same template a developer uses,
  not a bespoke CI-only config.** The pipeline runs `cp .env.example .env`
  and overrides only the secret values — it does not maintain a second,
  looser CI configuration file. The direct consequence (named explicitly in
  [ADR 0020](architecture/0020-local-stack-defaults-are-conservative.md)):
  CI runs the bridge as `Production`, loopback-bound, non-root, exactly
  like a real deployment — so a config-driven security regression (a
  setting that's only safe because `Production` is the default) gets caught
  in CI instead of surfacing for the first time in a real environment. A
  CI pipeline that's more permissive than production by convenience is
  testing a system that doesn't exist.
- **CI runs the real orchestrated multi-service stack, not a stubbed-down
  subset.** `docker compose up` brings up MariaDB, the legacy system, the
  seed/provisioning containers, and the bridge itself — the same
  integration-test layer from §10.1 that proves the legacy system actually
  accepts the bridge's registration runs here against the real thing, not
  a mock standing in for "CI is slow, so mock it there."

### 14.2 Secrets in the pipeline

- **Fall back to a known-fake secret when a real one isn't configured, and
  draw the value from the CI secret store first.** Every secret env var
  follows `${{ secrets.X || 'ci-fallback-value' }}` — a fork or a run
  without repository secrets configured still gets a deterministic,
  throwaway credential rather than an empty string that fails some other
  way downstream. This only works because nothing downstream (§9.1's
  `:?Set X in .env` enforcement) treats "set to a known-fake value" as
  equivalent to "unset" — the two failure modes stay distinguishable.
- **Never let the pipeline itself echo a secret value, even for
  debugging.** The step that writes `.env` explicitly prints `cut -d= -f1
  .env` — variable *names* only — specifically called out in a comment:
  "Print variable names only, never values." A CI log is frequently a
  lower-trust-tier artifact than the secret store feeding it (visible to
  more people, retained longer, sometimes cached) — treat it that way.

### 14.3 Generated-artifact drift as its own gate

- **Anything committed-but-generated gets a CI step that regenerates it and
  diffs against the commit — not just trust in whoever ran the generator
  last.** The committed OpenAPI client snapshot
  ([ADR 0031](architecture/0031-nswag-client-generated-from-a-committed-snapshot.md))
  is regenerated from a live instance and checked with `git diff --exit-code`
  in CI; an endpoint changed without re-running the generator fails the
  build. Generalizes to any legacy-auth bridge with a generated client, a
  committed schema snapshot, or a generated OpenAPI/GraphQL document.
- **Order that check correctly relative to the stack's own startup
  dependencies.** The regeneration script needs the legacy system reachable
  (it drives the bridge's own `OpenEmrReadinessGate`, §9.3) — so the
  workflow comment states explicitly that this step must run *after* "Start
  stack," not before. CI step ordering should mirror the same dependency
  graph as runtime startup ordering; get it backwards and the check fails
  for the wrong reason every time.

### 14.4 Readiness and teardown as CI citizens

- **Reuse the orchestrator's own healthchecks as the CI wait condition,
  instead of reinventing polling in pipeline script.** `docker compose up
  --wait` blocks on the exact `healthcheck:` definitions already in
  `docker-compose.yml` (§9.9) — one readiness definition serves local dev
  and CI alike, rather than a separate CI-only wait loop that can drift
  from what the healthcheck actually checks.
- **Give a slow-starting dependency a generous, explicit wait timeout in
  CI, bounded by an overall job timeout.** `--wait-timeout 1200` (20
  minutes, matching the generous `start_period` the legacy system's own
  healthcheck needs on a cold boot) sits inside a 45-minute job timeout —
  slow-but-working first boots aren't penalized, but a truly hung run still
  gets killed rather than burning the runner indefinitely.
- **Add a dedicated, best-effort diagnostics step that runs even when
  earlier steps failed**, separate from the actual test assertions. The
  "Probe endpoints" step (`if: always()`) curls both readiness endpoints
  and runs `docker compose ps` purely to leave a readable trail in the CI
  log; "Show compose logs on failure" (`if: failure()`) dumps container
  logs only when something actually broke. Diagnostics and assertions are
  different steps with different `if:` conditions, not bolted together.
- **Tear the stack down unconditionally, including its volumes.**
  `docker compose down -v` runs with `if: always()` — a failed run doesn't
  leave stale state for the next one, the same "resettable" requirement
  [`docs/environments.md`](environments.md) states for every environment
  tier, enforced here at the CI-job level specifically.

### 14.5 Supply chain and versioning, pinned into the pipeline

- **Pull the sanitized fixture data by a pinned, versioned release
  reference, never commit it into the repository.** CI downloads the
  database snapshot and site files from a tagged GitHub release
  (`SNAPSHOT_TAG: snapshot-openemr-8.0.0.3-ci`) rather than storing a
  binary DB dump in git history — deterministic and reproducible per CI
  run, without bloating the repository, and the tag itself documents
  exactly which legacy-system version the fixture was captured against
  (the same coupling §9.9 names for `OPENEMR_IMAGE`'s tag-not-digest
  pinning).
- **Re-apply fresh, run-specific credentials onto a restored static
  snapshot, rather than baking credentials into the snapshot itself.**
  `scripts/stack/apply-credentials.sh` rewrites the restored database's
  credentials to match the current run's generated `.env` values — so the
  same snapshot artifact is reusable indefinitely without ever needing its
  own baked-in secret rotated.
- **Pin the toolchain version from one file CI and local dev both read.**
  `actions/setup-dotnet` reads `global.json` rather than specifying a
  version inline in the workflow — one source of truth for the SDK version,
  avoiding the class of bug where CI silently uses a newer/older SDK than
  what a developer's machine resolves locally.

### 14.6 Pipeline hygiene

- **Grant the workflow's token the minimum scope it needs.** `permissions:
  contents: read` at the workflow level, rather than the broader default —
  this job only checks out code and downloads a release asset; it has no
  business writing to the repository or anything else.
- **Cancel superseded runs instead of letting them queue and consume
  capacity.** `concurrency: { group: ci-<workflow>-<ref>, cancel-in-progress:
  true }` matters specifically for a pipeline this expensive — standing up
  a full multi-container legacy-system stack per run — where three rapid
  pushes to the same branch queuing three full stack-boot-and-test cycles
  is pure waste.
- **Upload structured test results as a build artifact, not just console
  output.** `.trx` files are uploaded with `if-no-files-found: ignore`
  (`if: always()`), so a partial or failed run still leaves an inspectable,
  durable record, and the step itself never fails the build just because a
  truly broken earlier step produced no test output at all.

## 15. Docker and containers

§9.9 covers Dockerfile-level choices (multi-stage builds, non-root users,
image pinning). This section is the orchestration layer around them —
`docker-compose.yml`'s networking, volumes, and dependency graph — plus two
gaps worth naming the same way §12.5 named one in the test suite.

### 15.1 Container networking: two addresses for every service, on purpose

- **Compose's embedded DNS resolves service names to container-to-container
  traffic; published ports are for the host (or a browser) only — and a
  legacy-auth bridge touches both simultaneously.** AuthFacade reaches the
  legacy system at `OpenEmr__BaseUrl: "http://openemr:80"` — the Compose
  service name, resolved only inside the Compose network — while the
  browser must reach the gateway at `https://localhost:${GATEWAY_HTTPS_PORT}`,
  a published host port (`docker-compose.yml`). This is the mechanism
  behind §9.6's internal/external addressing rule: know, for every
  container-to-container call, whether it's a Compose-internal hop (service
  name, never a published port) or something a browser/external caller must
  resolve (a published, host-reachable address) — the two are never
  interchangeable, and this project hit that distinction as a recurring bug
  class (ADR 0012, 0026, 0034) before naming it explicitly.
- **Pin the network's subnet when any container-level logic parses an
  incoming address against it.** Covered in §9.6; restated here because
  it's a *networking* decision as much as a security one — the trusted-proxy
  middleware (§12.2) parses `context.Connection.RemoteIpAddress` against a
  CIDR that only stays meaningful because `docker-compose.yml`'s
  top-level `networks:` block fixes it instead of taking Compose's
  per-project dynamic default.

### 15.2 Volume types: match the mount to what the data actually needs

| Volume | Type | Mount mode | Why |
|---|---|---|---|
| `databasevolume` | Named | read-write | MariaDB's own files — must outlive container recreation, has no reason to be visible as host files |
| `sitesvolume` | Named | read-write | The legacy system's own site data/keys — same durability need, shared read-write across the legacy container and the PHP-based seed container that needs its `CryptoGen` keys |
| `./certs` | Bind mount | **read-only** (`:ro`) | Host-authored input (generated once by a setup script, §9.2); the running container only ever needs to read it |
| `./scripts/seeds/*.php`, `*.sh` | Bind mount | **read-only** (`:ro`) | Host-authored provisioning code — a compromised seed container gaining write access to its own script would be a privilege-escalation path for nothing the container legitimately needs |

The pattern: **named volumes for state the container itself owns and
mutates; read-only bind mounts for anything the host hands the container as
input.** Getting this backwards (a bind-mounted database directory, or a
writable mount for a provisioning script) either loses durability or widens
what a compromised container can modify — see §9.9's read-only-mount bullet
for the security framing of the second half.

### 15.3 `depends_on` is three different relationships — pick deliberately

Compose's `depends_on` condition is not one thing; this stack uses all
three, each for a reason:

- **`condition: service_healthy`** — wait for the dependency's own
  healthcheck, not just its process starting. `openemr` depends on `mysql`
  this way, and `auth-facade` depends on `openemr` this way
  (`docker-compose.yml`) — appropriate whenever the dependent would fail
  outright against a dependency that's merely "running" but not yet
  actually serving (§9.9's healthcheck bullet).
- **`condition: service_completed_successfully`** — wait for a one-shot
  container to *finish*, not to become healthy (it has no long-running
  health to check). `auth-facade` depends on both `seed` and
  `seed-authfacade-db` this way — the mechanism, concretely, behind §9.3's
  "bootstrap is its own idempotent step, never assumed to have run" rule:
  Compose itself refuses to start the dependent until provisioning has
  actually completed, rather than the ordering being a documentation
  promise someone could run out of sequence.
- **Plain `depends_on` (start order only, no wait)** — the gateway's
  dependency on `auth-facade` and `webapp` uses this form: it affects only
  the order containers are *created* in, with no health or completion gate.
  Appropriate only when the dependent can tolerate the dependency not being
  ready yet — here, because the gateway's own requests to either backend
  go through the same retry-worthy HTTP semantics (and, for AuthFacade
  specifically, the BFF proxy's own circuit breakers, §4) that a missing
  health-gate would otherwise need to substitute for. Using this form where
  a real readiness dependency exists is the mistake to watch for: it looks
  identical to `service_healthy` in the YAML's shape, differing only in one
  easy-to-miss keyword.

### 15.4 Two gaps worth naming explicitly

Following §12.5's own practice — naming an absence plainly instead of
implying full coverage:

- **No resource limits are set anywhere in `docker-compose.yml`.** No
  service declares a `deploy.resources.limits` (CPU/memory) block. For a
  local/CI stack this is a reasonable simplification; it is explicitly
  **not** acceptable at the production bar §11 sets ("resource limits...
  set from an actual load test"), and moving from here to there means
  adding those limits deliberately, not inheriting "no limit" as if it were
  a tested default.
- **No container image scanning step exists in `.github/workflows/ci.yml`.**
  §14's CI pipeline builds and runs every image but never scans one for
  known vulnerabilities (no `docker scout cves`, `trivy image`, `grype`, or
  equivalent step). §11's supply-chain checklist item ("dependency/image
  scanning runs in CI") is, concretely, this exact gap in this exact
  pipeline today — stated here the same way §12.5 names the untested
  `ReturnUrlValidator`: a short, obviously-addable step that simply isn't
  there yet, not a design decision.

### 15.5 Useful Docker / Compose commands for this shape of stack

```bash
# Validate and print the fully resolved config (env substitution, merges) without starting anything.
docker compose config

# Bring up the stack, blocking until every service's own healthcheck (or
# service_completed_successfully, for one-shot containers) passes — the same
# condition CI's "Start stack" step relies on (§14.4).
docker compose up -d --wait --wait-timeout 1200

# Bring up just one service and its dependency chain (useful for iterating on
# one container without rebuilding/restarting everything else).
docker compose up -d --build auth-facade

# Tail logs for one service, or every service, live.
docker compose logs -f auth-facade
docker compose logs -f

# Show each service's current health/state at a glance.
docker compose ps

# Run a one-off command inside a running container (e.g. a shell, or a
# bundled client) without modifying the image.
docker compose exec auth-facade /bin/sh

# Re-run a one-shot provisioning container by name (needed if Compose's own
# change-detection doesn't trigger it — the exact gap ADR 0037, §9.3 names).
docker compose up seed

# Tear down and remove volumes too — the same unconditional cleanup §14.4's
# CI step does, useful locally after changing seed data or schema.
docker compose down -v

# Scan a built image for known vulnerabilities (addresses §15.4's named
# gap; run manually today, since CI does not run this yet).
docker scout cves auth-facade:latest
# or, if Docker Scout isn't available:
trivy image auth-facade:latest

# Reclaim local disk space from stopped containers, dangling images, and
# unused build cache — safe to run between unrelated projects' stacks.
docker system df
docker system prune
```

### 15.6 Root considerations, in more depth

§9.9 states the rule ("run as the image's non-root user by default"); this
expands on *why*, *where it actually applies in this stack*, and *where it
doesn't* — root-in-container is a boundary that's easy to think of as one
on/off switch when it's actually several separate decisions.

- **What `USER $APP_UID` actually is, and why it needs no extra setup
  here.** The official `mcr.microsoft.com/dotnet/aspnet` runtime images
  ship a built-in, unprivileged `app` user baked into the image itself;
  `$APP_UID` is a build-arg the base image defines, not something this
  repo's Dockerfiles invent. Every custom Dockerfile
  (`src/Services/AuthFacade/Dockerfile`, `.../Gateway/Dockerfile`) switches
  to it with one line, in the *runtime* stage only — the build stage above
  it still runs as root (the default for `dotnet/sdk`), which is normal and
  low-risk, since nothing from that stage ships: only `COPY --from=build
  /app/publish .`'s output does.
- **Binding to a privileged port (<1024) is the classic reason a container
  "needs" root, and this stack sidesteps it by never using one.** A
  non-root process cannot normally bind port 80 or 443 without the
  `CAP_NET_BIND_SERVICE` capability. Every service here listens on 8080
  (and 8443 for the gateway's HTTPS) — ports Compose then remaps to the
  host-facing 80/443-shaped addresses (`OPENEMR_HTTPS_PORT`,
  `GATEWAY_HTTPS_PORT`, etc., §9.6) via its own port-publishing, which
  happens at the Docker daemon/host level and needs no privilege inside
  the container at all. Picking high ports inside the container is what
  makes "just run as non-root" possible without also granting a
  capability back — know this tradeoff explicitly if a future service
  seems to "need" a privileged port: it almost always doesn't, once the
  mapping happens outside the container instead of inside it.
- **Bind-mount permission friction is the other classic non-root pain
  point, and this stack avoids it by making every bind mount read-only.**
  A non-root container process writing to a bind-mounted host directory
  commonly hits a UID/GID mismatch (the host file is owned by a UID the
  container's non-root user doesn't have write access to). Every bind
  mount here (`./certs:/certs:ro`, the seed scripts) is `:ro` (§15.2) —
  sidestepping the write-permission problem entirely rather than solving
  it, because nothing mounted this way is ever written to from inside a
  container.
- **Non-root discipline applies to images this repo *builds*; it does not
  reach into images this repo only *consumes*.** `auth-facade`, `gateway`,
  `webapp`, and `login-ui` are built from this repo's own Dockerfiles and
  each explicitly sets a non-root `USER`. `mysql`, `openemr`, and
  `phpmyadmin` run unmodified third-party images (`.env.example`'s pinned
  `MARIADB_IMAGE`/`OPENEMR_IMAGE`/`PHPMYADMIN_IMAGE`), and
  `docker-compose.yml` sets no `user:` override for any of them — each
  runs as whatever its own upstream Dockerfile defaults to (commonly root
  for initialization, sometimes dropping to an unprivileged user before
  the main process starts, entirely at that image's own discretion). **A
  "we run non-root" claim for a legacy-auth migration needs to specify
  which containers it actually covers** — it is a per-image property, not
  a property of the stack as a whole, and a legacy system's own vendored
  image is usually the one container in the stack this project has the
  least control over.
- **One-shot provisioning containers are a different risk profile than a
  long-running root process, even if both technically run as root.**
  Whatever user `seed` and `seed-authfacade-db` run as (inherited from the
  `openemr` and `mariadb` images, per the bullet above), each exists only
  to run one script to completion and exit (`restart: "no"`, §9.9/§15.3) —
  a narrower, time-boxed exposure window than a web-facing process that
  stays up and keeps accepting requests as root for the life of the
  container. Don't apply the same scrutiny uniformly; a root-at-startup,
  exits-in-seconds provisioning container and a root, always-listening API
  server are not the same risk.
- **Further hardening beyond "non-root," not yet applied here, named as a
  gap in the same spirit as §15.4:** no service sets
  `security_opt: ["no-new-privileges:true"]`, drops Linux capabilities
  (`cap_drop: ["ALL"]`, adding back only what's actually needed), or runs
  with a read-only root filesystem (`read_only: true`, with explicit
  `tmpfs`/volume mounts for the few paths that must stay writable). Each
  is a real step past "don't run as root" that a production deployment
  (§11) should evaluate per service — a non-root process can still do
  damage with an unneeded capability or a writable root filesystem it
  doesn't need.

## References

- ADR index: [`docs/architecture/README.md`](architecture/README.md)
- Capstone design doc: [ADR 0047](architecture/0047-the-identity-bridge-design.md)
- Removal of the stand-in: [ADR 0035](architecture/0035-password-grant-and-bearer-auth-are-removed.md)
- Multi-instance limits: [ADR 0028](architecture/0028-browser-login-and-session-assume-a-single-instance.md)
- Session revocation: [ADR 0044](architecture/0044-session-store-backs-the-cookie-for-revocation.md), [ADR 0045](architecture/0045-silent-refresh-and-admin-session-revocation.md)
- Roadmap and open decision gates: [`ROADMAP.md`](../ROADMAP.md) (see M3: "ADR: whether AuthFacade stays as the permanent legacy bridge or is replaced by a dedicated identity service")
- Related memory: AuthFacade rename is deferred until that same M3 decision gate is resolved.

## Appendix A: .NET-specific libraries, tools, and commands

The body of this framework is deliberately stack-agnostic. This appendix is
the opposite on purpose: exactly what this codebase uses, and the `dotnet`
commands that exercise it — a concrete starting point if your own
legacy-auth bridge happens to be .NET, read from the actual
`.csproj` files under `src/` and `tests/`, not a generic library survey.

### A.1 Libraries in use, by purpose

| Purpose | Package(s) | Where |
|---|---|---|
| OIDC/OAuth client, browser flow | `OpenIddict.Client`, `OpenIddict.Client.AspNetCore`, `OpenIddict.Client.SystemNetHttp` | AuthFacade.Api — drives the authorization-code+PKCE flow against the legacy system (§2, ADR 0026) |
| OpenIddict's own state-token persistence | `OpenIddict.EntityFrameworkCore` | AuthFacade.Api — backs the state-token store that otherwise defaults to a per-process SQLite file (ADR 0026, §3.2) |
| Session cookie ticket persistence | `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` | AuthFacade.Api — the Data Protection key ring behind `AuthFacadeSession` (ADR 0043/0044) |
| Session/state database access | `Microsoft.EntityFrameworkCore.Sqlite` (dev fallback), `Microting.EntityFrameworkCore.MySql`, `MySqlConnector` | AuthFacade.Api, OpenEmr.Data — the dev-fallback-vs-real-backend fork from §9.1 (`AuthFacadeDbOptions.IsConfigured`) |
| Certificate loading (persisted OpenIddict certs) | `System.Security.Cryptography.Pkcs` | AuthFacade.Api — loading the PKCS#12 files from §9.2/ADR 0046 |
| Resilience (circuit breakers) | `Polly` / `Polly.Core`, `Microsoft.Extensions.Http.Polly` | AuthFacade.Api, WebApp — the shared-pipeline pattern in §9.5/ADR 0041; see ADR 0035's note on the v7/v8 compatibility shim it later dropped |
| Reverse proxy | `Yarp.ReverseProxy` | Gateway (the single external origin, ADR 0033), and AuthFacade.Api itself (the BFF proxy, ADR 0029) |
| JWT parsing/validation | `System.IdentityModel.Tokens.Jwt` | AuthFacade tests (`FakeOpenEmrServer`, §10.2) and wherever `id_token` claims are read |
| OpenAPI document + interactive docs UI | `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore` | AuthFacade.Api — development-only (§9.1: gated behind `IsDevelopment()`) |
| Typed client generation from a committed snapshot | NSwag (invoked via `scripts/dev/generate-authfacade-openapi.sh`, not a `PackageReference`) | Produces `OpenEmrNext.AuthFacade.Client` from `AuthFacade.json` (ADR 0031, §14.3) |
| Options binding/validation | `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.Extensions.Options.DataAnnotations` | AuthFacade.Composition — the `ValidateDataAnnotations().ValidateOnStart()` pattern in §9.1 |
| Blazor UI components | `MudBlazor` | WebApp, LoginUi |
| Testing framework | `xunit`, `Microsoft.NET.Test.Sdk` | All three test projects |
| Code coverage data collection | `coverlet.collector` | All three test projects (A.3) |

### A.2 Tools worth knowing about

- **`global.json`** pins the SDK (`10.0.100`, `rollForward: latestFeature`)
  — the single source of truth both local dev and CI's
  `actions/setup-dotnet@v4` read (§14.5). Run `dotnet --version` in the
  repo root to confirm the resolved SDK matches what you expect; a
  mismatch here is a common source of "works on my machine" drift.
  Note this repo pins only the SDK, and the installed SDK bundles exactly
  one runtime compatible with the apps' own `net10.0` target framework —
  no separate container needed to confirm the runtime.
- **Scalar** (`Scalar.AspNetCore`) is the interactive OpenAPI UI, reachable
  at `/scalar/v1` only when `ASPNETCORE_ENVIRONMENT=Development`
  (`Program.cs`, §9.1) — a lighter-weight alternative to Swagger UI, used
  here purely as a developer convenience, never shipped to a real
  environment.
- **NSwag** generates `OpenEmrNext.AuthFacade.Client` from the committed
  `AuthFacade.json` snapshot. It is not a `PackageReference` in this
  codebase — it's invoked as a script (`scripts/dev/generate-authfacade-openapi.sh`)
  against a live, throwaway instance, then diffed against the commit in CI
  (§14.3). If you adopt this pattern elsewhere, remember the generator
  needs a *running* instance, not just the source code, to regenerate
  against.
- **ArchUnitNET** is named in `ROADMAP.md` (M6) as a planned adoption for
  architecture tests, **not yet in use**: today's `LayerDependencyTests`
  (§10.1) is hand-rolled reflection over `Assembly.GetReferencedAssemblies()`,
  which works fine at this solution's current size. If your own bridge's
  layering rules outgrow a few hand-written reflection checks, ArchUnitNET
  (or NetArchTest) expresses the same kind of rule — "layer A must never
  reference layer B" — more fluently at scale.
- **No `dotnet-tools.json` manifest exists in this repo** — there are no
  project-local CLI tools (`dotnet tool restore`) pinned anywhere. Any
  tool used below (`dotnet-reportgenerator-globaltool`, for example) would
  need either a global install or a manifest added deliberately; it isn't
  assumed to already be present on a clean checkout.

### A.3 Helpful `dotnet` commands

**Build and restore**

```bash
# Restore and build the whole solution, matching CI's own steps (§14).
dotnet restore OpenEMR-Next.slnx
dotnet build OpenEMR-Next.slnx --configuration Release --no-restore
```

**Running tests**

```bash
# Every test project in the solution.
dotnet test OpenEMR-Next.slnx --configuration Release --no-build --no-restore

# Just the fast, in-process layers (unit + architecture), skipping anything
# that needs the live Compose stack up.
dotnet test tests/Services/AuthFacade/OpenEmrNext.AuthFacade.UnitTests
dotnet test tests/Services/AuthFacade/OpenEmrNext.AuthFacade.ArchitectureTests

# The black-box integration layer — requires the stack to actually be running
# first (§10.1); this is the exact filter AuthFacadeStackTests.cs's own doc
# comment names.
dotnet test tests/Services/AuthFacade/OpenEmrNext.AuthFacade.IntegrationTests --filter Category=Integration

# One test by name, useful while iterating on a single security-test case (§12).
dotnet test --filter "FullyQualifiedName~Revoking_a_session_signs_it_out_immediately"
```

**Coverage**

```bash
# Collect coverage via the coverlet.collector already referenced in every
# test project (A.1) — no extra package install needed for this step alone.
dotnet test OpenEMR-Next.slnx --collect:"XPlat Code Coverage" --results-directory TestResults

# Turn the resulting Cobertura XML into a browsable HTML report. Requires a
# one-time global tool install (not pre-provisioned, per A.2's note).
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"TestResults/coverage-report" -reporttypes:Html
```

**Security / dependency scanning**

```bash
# Known-vulnerable NuGet packages, including transitive ones — the
# dependency half of §11's "image scanning runs in CI" requirement, scoped
# to .NET packages rather than container images (§15.4 covers the image half).
dotnet list package --vulnerable --include-transitive

# Deprecated packages (often a precursor to an unpatched vulnerability later).
dotnet list package --deprecated

# Outdated packages generally — not a security signal by itself, but the
# list to review before deciding a vulnerable package's fix is "upgrade."
dotnet list package --outdated

# A restore-time NuGet audit also runs by default on recent SDKs and prints
# warnings for known vulnerabilities as part of an ordinary restore/build —
# worth not silencing those warnings in CI even if they're noisy at first.
dotnet restore OpenEMR-Next.slnx
```

**Formatting and static analysis**

```bash
# Check formatting without modifying files (good for a CI gate); drop
# --verify-no-changes to actually apply fixes locally.
dotnet format OpenEMR-Next.slnx --verify-no-changes

# Surface analyzer warnings as build errors temporarily, to see everything
# at once without permanently flipping TreatWarningsAsErrors in the repo.
dotnet build OpenEMR-Next.slnx -warnaserror
```

**Schema (once §9.10/§11 moves past `EnsureCreated`)**

```bash
# Requires the EF Core tools (dotnet tool install -g dotnet-ef) and a
# DbContext-aware startup project.
dotnet ef migrations add <Name> --project src/Services/AuthFacade/OpenEmrNext.AuthFacade.Api
dotnet ef database update --project src/Services/AuthFacade/OpenEmrNext.AuthFacade.Api
```

**Other**

```bash
# Confirm the SDK global.json resolves to locally (§A.2).
dotnet --version

# Regenerate and verify the committed OpenAPI client snapshot — the exact
# check CI runs in §14.3, runnable locally before pushing.
bash scripts/dev/generate-authfacade-openapi.sh
git diff --exit-code -- src/Services/AuthFacade/OpenEmrNext.AuthFacade.Client/OpenAPIs/AuthFacade.json

# Run one service directly (outside Docker) for fast local iteration —
# picks up appsettings.Development.json and falls back to the dev-only
# defaults named throughout §9.1.
dotnet run --project src/Services/AuthFacade/OpenEmrNext.AuthFacade.Api
```
