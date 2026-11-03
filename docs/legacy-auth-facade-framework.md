# Framework: migrating authentication behind a legacy system

A practical guide to putting a modern login experience in front of an old
system that already knows who your users are — without rebuilding its user
accounts, and without accidentally creating a second, conflicting source of
truth about identity.

It is deliberately generic: no specific codebase, vendor, language, or
framework. Every pattern here can be implemented in any stack.

## Start here

### The problem, in plain terms

You have an old system ("the legacy system"). It holds the real user
accounts and passwords, and it's the only thing that can truthfully answer
*"is this person who they say they are?"* You now want to build something
new next to it — a new web app, a new API, eventually new services — and
those new things need to know who the user is.

You have three options, and only one of them is usually a good idea:

1. **Copy the user accounts into a new system.** Now two systems both
   think they own the truth about identity, and they will drift apart.
   Expensive, risky, and rarely necessary at the start.
2. **Let every new app talk to the legacy system's login directly.** Now
   every new app has to understand the legacy system's quirks, and every
   quirk gets reimplemented (differently) in each one.
3. **Build one small piece in the middle.** It is the only thing that
   speaks the legacy system's login language. Everything new talks to
   *it* instead, in a modern, standard way. That piece is what this
   document calls a **facade** (also "bridge," or "BFF" —
   backend-for-frontend).

This framework is about option 3: how to build that middle piece, what
decisions you will face, how to secure it, how to test it, and how to tell
when it's actually ready for real users.

### The shape of what you're building

```text
                    ┌──────────────────┐
   browser ────────▶│   The facade     │   Holds the session. Speaks the
   (your new app)   │  (what you build)│   legacy system's login language
                    └────────┬─────────┘   so nothing else has to.
                             │
                             ▼
                    ┌──────────────────┐
                    │  Legacy system   │   Owns the real accounts and
                    │   (unchanged)    │   passwords. Answers "is this
                    └──────────────────┘   person who they claim to be?"
```

The two rules that make this work, and that most of this document is
elaborating:

- **The facade never stores passwords or user accounts.** It asks the
  legacy system, every time. It is a translator, not a second source of
  truth.
- **The browser gets a session, not a key to the kingdom.** The facade
  keeps anything genuinely powerful (the legacy system's own access
  tokens) on the server side, and hands the browser only a meaningless
  reference to its session.

### Who this is for

- **Engineers** building or reviewing that middle piece.
- **Tech leads / architects** who need to make the handful of big
  decisions in §3 and defend them later.
- **Anyone inheriting** an existing facade who needs a checklist of what
  should be true of it (§4, §15).

You don't need prior OAuth/OIDC expertise. Terms are defined in the
glossary below, and introduced gently on first use.

### How to read this document

**If you're new to this problem**, read in this order and stop after step 3
on your first pass:

1. The glossary below — just skim it, then refer back as needed.
2. "The core strategy" — the five stages a facade goes through, and why
   the order matters.
3. §1 and §2 — the questions to answer before designing anything.
4. §3 — the decision graphs. These are the choices that are expensive to
   change later.
5. Everything else is reference material, best read as working checklists
   while you build (§4, §9–§14) and as a gate before going live (§15).

**If you've built one of these before**, §3 (decision graphs), §6
(tradeoffs), §7 (accepted-risk ledger) and §15 (production readiness) are
the sections most likely to contain something you haven't already
internalized.

**If you're ready to start building**, this document tells you what the
decisions are; its companion
[`auth-facade-implementation-guide.md`](auth-facade-implementation-guide.md)
tells you what to build in what order, as a sequential set of steps with
verifiable finish lines. Use that as your working document and come back
here when it points you at a decision.

### Terms used in this document

Skim this once; come back when a term trips you up. Nothing here requires
memorizing.

| Term | What it means, plainly |
|---|---|
| **Legacy system** | The old system that owns the real user accounts and passwords. Assumed unchanged throughout. |
| **Facade** (bridge, BFF) | The small new component in the middle. The only thing that speaks the legacy system's login language. "BFF" = backend-for-frontend: a backend that exists specifically to serve one frontend. |
| **Identity provider (IdP)** | Whatever actually verifies credentials and issues proof of identity. Here, that's the legacy system — *not* your facade. |
| **OAuth2 / OIDC** | The industry-standard protocols for "let this app know who the user is, without giving it their password." OIDC (OpenID Connect) is a layer on top of OAuth2 that adds identity. If the legacy system already speaks these, your job is much easier. |
| **Authorization-code flow** | The standard, safe login dance: the app sends the user to the IdP's own login page, the user logs in *there*, and the IdP sends the app back a short-lived one-time **code**, which the app exchanges (server-to-server) for tokens. The user's password never touches your app. |
| **PKCE** | ("pixy") An add-on to the authorization-code flow that stops an attacker who intercepts the code from being able to use it. Required for apps that can't keep a secret (browsers, mobile). |
| **Credential/password grant** | The old, discouraged shortcut: your app collects the user's password and sends it to the IdP itself. Simple to build, which is why it shows up as a temporary stand-in — and why this document keeps telling you to delete it. |
| **Access token** | A short-lived string that proves "the bearer of this is allowed to call this API as this user." Powerful; treat like a password. |
| **Refresh token** | A longer-lived string used to get a new access token without making the user log in again. Even more sensitive than an access token. |
| **Bearer token** | Any token where simply *holding* it is sufficient to use it — no further proof required. Hence: never give one to a browser if you can avoid it. |
| **Session cookie** | A small value stored by the browser and sent on every request, identifying the user's logged-in session. |
| **Opaque session reference** | A session cookie whose contents mean *nothing* on their own — just a lookup key into a record on your server. Preferred, because you can delete the server record to kill the session instantly. |
| **Self-contained (stateless) session** | The opposite: a cookie that carries the actual encrypted session data inside it. No database needed, but you can't revoke it — the browser holds a valid session until it expires on its own. |
| **Revocation** | Deliberately ending a session or token before it expires naturally (e.g. because a laptop was stolen). |
| **Scope** | The specific permissions an app asks for when requesting a token ("read the user's name," "call the patient API"). Ask for the least you need. |
| **Claim** | A single fact inside a token ("the user's ID is 42," "this token expires at 3pm"). |
| **Rate limiting** | Refusing requests from the same source beyond N per time window, so nobody can hammer your login endpoint. |
| **Circuit breaker** | A wrapper around calls to a dependency that, after enough failures, stops trying for a while and fails immediately instead. Prevents one sick dependency from hanging every request in your system. |
| **Reverse proxy / gateway** | A component that receives requests and forwards them to the right backend. Used here to give everything one shared public address. |
| **Forwarded headers** | When a proxy sits in front of your app, it tells the app the *original* client's address and protocol via headers like `X-Forwarded-For`. These are trivially forged, so they must only be trusted from the proxy itself. |
| **Key ring** | The set of cryptographic keys your app uses to encrypt/sign things like session cookies. If it's regenerated each time the app restarts, every existing session breaks. |
| **Liveness vs. readiness** | "Is the process running?" vs. "can it actually serve a request right now?" Different questions; often wrongly treated as one. |
| **Idempotent** | Safe to run more than once with the same result. Important for setup/provisioning scripts. |
| **Stand-in** | Any deliberately temporary implementation you build to unblock progress, intending to delete it. The password grant is the classic one here. |

If you want to see this framework applied to a real system, with the
actual decision records behind each choice, see the companion document in
this repository. This file stands on its own.

## Scope

This framework covers **the facade/bridge service itself**: delegated
credential verification, session and token handling, and the
hardening/testing/operational concerns around that one component. It is
**not** a framework for a full legacy-auth migration end to end. Explicitly
out of scope:

- **Authorization** beyond authentication — what a signed-in identity may
  do with any system's data. Treated below as a separate, unbuilt layer the
  facade should never be assumed to provide.
- **User/account migration** — moving, merging, or deduplicating accounts
  into a new system of record. The facade pattern below never becomes the
  account store unless you deliberately choose that fork (§3.4).
- **MFA, password reset, account recovery**, and other identity-lifecycle
  flows not already provided by the legacy system itself.
- **Broader service decomposition** (splitting a monolith into bounded
  contexts, per-service databases, event-driven integration) — only
  touched here where it intersects with identity (§5).

## The core strategy

A legacy system already owns identity — real accounts, real credential
checks, often a real OAuth/OIDC server. The temptation is to build a new,
independent identity system "the right way" and migrate users into it. The
far cheaper and lower-risk strategy, and the one this framework assumes:

**Delegate, don't duplicate.** Build a thin facade that verifies identity by
asking the legacy system, never by holding a second, independently-correct
copy of credentials or accounts. The facade's job is to translate the
legacy system's own authentication into something a modern client (a
browser, a new backend) can safely consume — nothing more.

This strategy typically unfolds in five stages. The order matters, and
each one is a deliberate decision — not a default to drift into.

**Stage 1 — Build a stand-in, and label it temporary on day one.**
Early on you need *something* working so the rest of the team isn't
blocked. The fastest path is usually the discouraged one: collect the
password yourself and hand it to the legacy system (the credential/password
grant). That's acceptable as scaffolding.
*Why it's a trap:* scaffolding that works tends to become permanent.
*What to do:* write down, in the same change that adds it, the condition
under which you will delete it ("when the real browser login works end to
end"). A removal criterion written later never gets written.

**Stage 2 — Build the real login flow.**
Switch to the authorization-code flow (plus PKCE) against the legacy
system's own login page. The user types their password into the legacy
system, not into your app.
*Why:* your facade never sees, stores, logs, or accidentally leaks a
password it never received in the first place. This single change removes a
whole category of risk rather than mitigating it.

**Stage 3 — Keep the powerful tokens on the server.**
Once login works, the facade is holding real access and refresh tokens for
the user. Do not pass them to the browser. Give the browser an opaque
session reference instead, and have the facade attach the real token
server-side when it needs to call the legacy system on the user's behalf.
*Why:* a token in a browser can be stolen by any script that runs on your
page. A session reference, stolen, can be revoked (stage 5); a stolen
token usually cannot.

**Stage 4 — Delete the stand-in from stage 1.**
Once nothing calls the old path, remove it: the endpoint, its
configuration, its tests, and any code that existed only to support it.
*Why:* an unused login path is still a login path. It's attack surface
nobody is monitoring, maintained by nobody, understood by nobody after six
months. "We might need it for mobile someday" is not a reason to keep it —
mobile needs the stage 2 flow too.

**Stage 5 — Harden, then prove it scales.**
Now add the things that make it operationally real: the ability to revoke
a session, resilience when the legacy system is down, an audit trail, and
keys/certificates that survive a restart. *Only then* test running more
than one copy of the facade.
*Why that order:* almost everything in stage 3 is quietly built assuming a
single process (see §3.2). Discovering that while also debugging a
production scaling incident is considerably worse than discovering it
deliberately.

Everything below elaborates one of these stages, or a decision inside one.
If you only remember two things from this document, remember **stage 3**
(the browser never holds a real token) and **stage 4** (delete the
stand-in).

## 1. Questions to ask stakeholders

Answer these before any architecture discussion — most migration rework
traces back to answering these late instead of first.

**Scope and ownership**
- Who today can answer "is this credential correct"? Is there more than
  one such system?
- Does the legacy system expose a standards-based protocol (OAuth2/OIDC,
  SAML) already, or only a proprietary login form/table?
- Who owns the user/credential data after migration — does it move, get
  duplicated, or stay exactly where it is, permanently? ("Stays put
  permanently" is a valid, durable answer.)
- What compliance or audit regime constrains where credentials or tokens
  may be logged, stored, or transit?

**Client population**
- Which clients need to authenticate today: browser, native mobile,
  server-to-server, third-party integrations? Get this in writing — do not
  build for a hypothetical client that doesn't exist yet.
- For each client type: does it need the legacy system's own token at all,
  or only an authenticated session with the new frontend?

**Operational constraints**
- Will this run as one instance, or must it scale horizontally / survive
  rolling deploys from day one? This changes the session-storage answer
  entirely (§3.2).
- What's acceptable when the legacy system is slow or down: fail closed,
  fail open, or degrade one specific feature?
- What is the actual credential policy already in production? Don't
  assume a clean policy — real accounts may predate any policy that
  exists today.
- Is there a way to revoke a compromised session *today*, without legacy
  system cooperation? Who is authorized to revoke someone else's session,
  and how?

**Timeline pressure**
- Is there a deadline that justifies a known-temporary stand-in? If yes,
  write its deletion criterion down *before* building it.

## 2. Architectural questions to resolve early

These are the questions your *team* needs to answer (as opposed to §1's,
which need a stakeholder). None of them has a universally right answer,
but each has a sensible default, given below. Answering them late is what
produces expensive rework — several of these decisions are cheap now and
very expensive after you have real users.

### Identity ownership
- Does the facade **verify** credentials (delegate to the legacy system)
  or **issue its own** identity assertions? Delegating is almost always
  the right default.
- Is the facade a **token broker** (hands a token to any caller), a
  **BFF** (holds the token server-side, gives browsers a cookie only), or
  a **dedicated identity service** (becomes the system of record itself)?
  Default to BFF; treat "dedicated identity service" as a deliberate,
  later fork (§3.4), not a starting assumption.

### Session model
- Where does session state live: a self-decrypting cookie (stateless,
  cannot be revoked without a blocklist) or a server-side record
  referenced by an opaque cookie key (stateful, revocable)? Start
  stateless only if you can state, in writing, that revocation isn't a
  requirement yet.
- Does the browser ever hold a real, legacy-recognized token, or only an
  opaque session reference? Letting the browser hold the real token is
  the single biggest blast-radius decision in the whole design — default
  to never.

### Token/credential handling
- If credentials must pass through the facade at all, what's the
  retrieved-once-and-cleared discipline, and is it tested? Prefer avoiding
  this entirely via a redirect-based flow where the facade never sees the
  password.
- Does the legacy system's token refresh transparently to the client, or
  does the client have to notice and re-authenticate? Prefer silent,
  server-side refresh with a safe fallback (serve the stale token, let the
  legacy system's own rejection decide).

### Multi-instance / deployment
- Everything "per-process" today (an ephemeral cert, a local temp file, an
  in-memory key ring) is a hidden single-instance assumption. Audit for
  all three before scaling past one replica: any state-token/correlation
  store, any encryption/signing certificates, and the session ticket's own
  key ring.

### Data ownership and boundaries
- Which database(s) hold what? Default: keep the legacy database
  untouched; give the facade its own database for session state only — two
  databases, two least-privilege users, no shared tables.

## 3. Decision graphs

The four decisions below are the ones that are hardest to reverse later, so
they're laid out as explicit trees rather than prose. Read each one by
starting at the top question and following the branch that matches your
situation; the leaf you land on is the recommendation, with the reasoning
alongside it.

If you're short on time, §3.1 and §3.2 are the two that shape everything
else.

### 3.1 How should the facade relate to the legacy identity system?

```text
Does the legacy system already speak a standard protocol (OIDC/OAuth2/SAML)?
│
├─ No → Will you be allowed to modify the legacy system?
│        ├─ Yes → Add a standard protocol endpoint to it (smaller blast
│        │        radius than building a parallel identity system).
│        └─ No  → Build a verification adapter in front of it (delegate
│                 credential checks; never duplicate the credential store).
│
└─ Yes → Do any clients need the legacy system's own token (bearer, direct
          API calls as that identity)?
          ├─ No  → Build a BFF: redirect-based login, session cookie only,
          │        token kept server-side. This is the default — prefer it.
          └─ Yes → Pass the legacy token through unchanged rather than
                    minting your own, AND still keep it off the browser if
                    the caller is itself a browser-facing app (attach the
                    token server-side on the one hop that actually needs
                    it, never hand it to the client).
```

### 3.2 Where does session state live?

```text
Must a session be revocable before natural expiry (compromise response,
admin action, logout-everywhere)?
│
├─ No, and single-instance is acceptable for the foreseeable scope
│   → A self-contained encrypted cookie is acceptable short-term. Write
│     down this limit explicitly so nobody assumes revocation exists when
│     it doesn't.
│
└─ Yes, OR you need >1 instance / rolling deploys
    → Server-side session record, opaque cookie key only. Needs: a shared
      backing store, a shared encryption/signing key ring, persisted
      (not ephemeral) certificates. Revocation = delete the row. No
      legacy-system cooperation required for your own side of the token.
```

### 3.3 Is it time to delete the stand-in?

```text
Does anything in the running system still call the stand-in path in
production traffic?
│
├─ Yes → Not yet. Keep it, but keep its removal criterion written down.
│
└─ No  → Is the reason you're keeping it a *real, scheduled* need, or a
          *hypothetical future* one?
          ├─ Hypothetical ("maybe mobile will need it later", "maybe we'll
          │   need M2M someday") → Delete it now. A real future need gets
          │   its own decision against whatever's actually required
          │   then — not a standing justification for keeping unused,
          │   deprecated auth surface around. Unused attack surface has
          │   no owner.
          └─ Real, scheduled → Keep it, scoped to that client only, and
                                revisit at the date it was scheduled for.
```

### 3.4 BFF forever, or a dedicated identity service?

```text
Does the facade need to become the system of record for identity (own
accounts, own password reset, own MFA enrollment) rather than only
brokering sessions against the legacy system?
│
├─ No → Stay a BFF/bridge. Cheaper, smaller attack surface, no duplicated
│        user data. Revisit only if a concrete requirement forces the
│        question (legacy system being decommissioned, a capability it
│        cannot support: SSO federation, per-tenant IdP, step-up auth).
│
└─ Yes → Dedicated identity service. Needs: a migration plan for existing
          credentials (hash-format compatibility or forced reset), a
          decision on who owns the OAuth client registration going
          forward, and a cutover plan for sessions already issued by the
          facade.
```

## 4. Hardening requirements

Treat this as a checklist of *categories*. A design that skips one should
say so explicitly as an accepted risk (§8), not silently.

| Category | Requirement |
|---|---|
| Transport | TLS terminated at a trusted boundary; redirect plain HTTP; HSTS; forwarded-header trust scoped to a known network, never blindly trusted |
| Credential handling | Never log credentials or tokens, at any log level, in any logger category; retrieve plaintext once and clear it if you must touch it at all |
| Rate limiting | Every unauthenticated entry point (login, callback) shares one budget per client address |
| Resilience | Circuit breakers around every outbound call to the legacy system (token exchange, key fetch, proxied calls), so one failing dependency degrades fast instead of hanging every caller |
| Session integrity | Cookie carries an opaque reference only; the real ticket (and any real legacy token) lives server-side |
| Revocation | A session can be killed server-side, both self-service and admin-initiated, with no cooperation from whoever holds the cookie |
| Logout | Logout ends the session on both sides when the legacy system supports it — read its actual contract from source/discovery, don't assume |
| Key/cert durability | Nothing security-critical is ephemeral-per-process: encryption keys, signing certificates, and any correlation/state store all survive a restart |
| Audit | Login outcomes and any proxied data access are logged to one filterable category, with zero credentials/tokens/bodies in it |
| Start-up ordering | The facade refuses to serve traffic before its dependencies (legacy system readiness, client registration, schema) are confirmed ready |
| Input limits | Bound every field to what the legacy schema can actually hold, as a guard, not a policy statement |
| Attack surface | Remove auth surface nothing calls (old grant types, old auth schemes) instead of leaving it live-but-unused |

## 5. Multi-service considerations

Apply these once there's more than one backend service behind the same
login:

- **One externally-reachable identity surface.** Every consumer — the
  frontend and the facade's own login/callback/session endpoints — shares
  one address, fronted by a gateway that makes no identity decisions of
  its own. Don't let each service grow its own login entry point.
- **Token validation belongs at the edge, once, not in every service.**
  Validating independently in N services is N places to get it wrong and
  N places to update when a signing key rotates.
- **The facade's own proxy should scale by adding routes to a gateway, not
  by learning every new service by name.** A browser session's calls go
  through one proxy cluster pointed at a gateway; new services arrive
  behind that gateway, not as new proxy destinations the facade must know
  individually.
- **Service-to-service calls need their own answer — don't reuse the
  browser's session mechanism for them.** Defer machine-to-machine auth to
  its own decision against whatever's actually needed when a real
  caller exists, rather than stretching the browser flow to cover it. A
  session cookie is not a service credential.
- **Authorization is a separate, not-yet-answered layer from
  authentication.** The facade answers "who is this," not "what may they
  do with service X's data" — call this out as its own workstream per
  service, not an extension of the facade.
- **Each new service inherits the facade's accepted risks (§8) until it
  actually needs the gap closed.** Don't silently assume a gap is closed
  just because time has passed; re-check it against the specific service's
  requirements.
- **Scope widening is explicit, not incidental.** Request only the scopes
  a real caller actually needs; widen per concrete requirement, never
  preemptively "to be safe for later."

## 6. Tradeoffs

Each row is a genuine fork where both options are defensible in some
context. The last column is what to pick absent a specific reason to do
otherwise — and if you deviate, that deviation is exactly the kind of
thing worth writing down in the ledger (§7).

| Choice | Pro | Con | Default recommendation |
|---|---|---|---|
| Token broker (mint your own token) vs. pass-through legacy token | Broker: stable contract for clients regardless of legacy quirks | Broker: you now own token semantics, expiry, and signing — a second source of truth for identity | Prefer pass-through, or better, no client-visible token at all (BFF) |
| BFF (cookie only) vs. handing clients a bearer token | BFF: browser never holds a credential worth stealing via XSS | BFF: every browser call must round-trip through the BFF/proxy; no direct client-to-legacy-API calls | BFF, for browser clients, unconditionally |
| Stateless cookie vs. server-side session record | Stateless: no database, scales trivially | Stateless: no revocation without a separate blocklist | Server-side, the moment revocation is a real requirement |
| Sticky sessions vs. shared state for multi-instance | Sticky: cheap, no new infrastructure | Sticky: a rolling deploy or replica failure reproduces the exact failure it was meant to hide | Shared state; sticky sessions only as a named, temporary stop-gap |
| Keep a deprecated grant "just in case" vs. delete once unused | Keep: zero risk of breaking an unknown caller | Keep: permanent attack surface and test burden for a caller that may never materialize | Delete; a real future need gets its own decision later |
| Build your own resilience primitive vs. reuse a library pattern | Reuse: the pattern survives even when the call site it guarded is deleted | Reuse: dead decorators pointed at deleted interfaces are not a safe intermediate state | Delete the wiring with the call site; keep the pattern to reapply |
| Fix multi-instance support now vs. defer | Now: no future surprise when scaling | Now: speculative work against a requirement that doesn't exist yet | Defer, with the exact preconditions for revisiting written down |

## 7. The accepted-risk ledger

The single most reusable practice in this framework: **maintain a document
that lists what was deliberately not fixed, next to what was**, so a risk
is either addressed or *consciously* owned — never just forgotten by
omission.

For any legacy-auth facade, maintain this ledger from day one and revisit
it at every milestone gate:

- What did we decide *not* to build, and why was that the right call *at
  the time*?
- What would have to be true for that decision to need revisiting?
- Who/what inherits this risk downstream (a new service, a new client
  type)?

A good ledger entry names the specific gap (e.g., "the legacy system
exposes no server-to-server revocation endpoint, so our own revocation
never reaches it"), not a vague "security could be improved."

## 8. Migration checklist

Use this phase-gated; some items only become relevant at a later phase.

**Phase 0 — before writing code**
- [ ] Answered every question in §1 with the actual stakeholder, in writing.
- [ ] Confirmed whether the legacy system exposes a standard protocol or
      only a proprietary login path.
- [ ] Decided: delegate credential verification, or build your own store?
      (Default: delegate.)
- [ ] Named any temporary stand-in explicitly, with its deletion criterion
      written down before it's built.

**Phase 1 — stand-in / unblock**
- [ ] Credentials, if they must pass through the facade at all, are never
      logged and cleared after single use.
- [ ] Integration tests cover the real legacy system, not a mock of it,
      for at least the credential-verification path.
- [ ] Scopes/claims requested are minimal, not "everything, to be safe."

**Phase 2 — real browser flow**
- [ ] Authorization-code (+ PKCE for public clients) replaces any
      password-in-the-client flow.
- [ ] The browser never receives the legacy system's own token in any
      response body.
- [ ] Session is established via a cookie; TLS, Secure, HttpOnly, SameSite
      are all actually true end to end.

**Phase 3 — delete the stand-in**
- [ ] Confirmed zero production callers of the stand-in path.
- [ ] Deleted the stand-in's code, tests, and any now-empty layers/modules
      — not left as dead scaffolding.
- [ ] Regenerated any committed API snapshot/client that referenced it.

**Phase 4 — harden**
- [ ] Rate limiting on every unauthenticated entry point.
- [ ] Circuit breakers on every outbound call to the legacy system.
- [ ] Session state is revocable server-side (self-service and admin
      paths).
- [ ] Logout ends the session on both sides, using the legacy system's
      real logout contract (verified, not assumed).
- [ ] Encryption keys, signing certs, and any state store survive a
      process restart.
- [ ] Audit log covers login outcomes and any proxied data access, with
      zero credentials/tokens in it.
- [ ] Start-up ordering: refuse traffic until dependencies are confirmed
      ready.

**Phase 5 — scale**
- [ ] Audited every "per-process" artifact (temp files, in-memory key
      rings, ephemeral certs) before running more than one instance.
- [ ] Shared backing store + shared key ring in place, or session affinity
      is an explicit, written-down interim measure.
- [ ] Verified live: restart mid-session, restart mid-login, confirm
      nothing breaks.

**Phase 6 — write the capstone**
- [ ] One document states the finished design's guarantees end to end.
- [ ] One section lists accepted risks explicitly, each with why it's
      accepted and what would change that.
- [ ] Downstream teams/services know they inherit those risks until they
      personally need the gap closed.

**Ongoing — every new service added behind the facade**
- [ ] Token validation happens at the edge/gateway, not reinvented per
      service.
- [ ] Service-to-service auth has its own decision, not a reuse of the
      browser session mechanism.
- [ ] Authorization (what, not just who) is designed per service; it is
      not inherited from the facade.

## 9. Setup and configuration decision points

Smaller, implementation-level choices that recur across almost any
facade, regardless of stack:

- **Distinguish, per setting, whether "absent" means "broken" or
  "safe."** Fail fast (refuse to start) on missing required connection
  details. Do *not* fail fast on a setting whose empty value is a
  legitimate, conservative default (an empty admin-allowlist that fails
  closed, for example) — validate accordingly, not with one blanket
  policy.
- **Let the absence of a configuration section select a safe fallback
  instead of a separate feature flag.** A database/certificate
  configuration that's "fully present or fully absent" can select, at
  runtime, between a real backend and a throwaway local fallback — one
  codebase for both a developer's machine and real infrastructure.
- **Enforce required secrets at the orchestration layer, not only in app
  code.** A deployment tool that refuses to start the whole stack when a
  required secret is unset is cheaper to diagnose than a container that
  starts, binds configuration, and only then fails deep in a stack trace.
- **Default every environment-sensitivity switch to the conservative
  setting**, with explicit opt-in to anything looser (default to a
  production-like mode; gate any debug/introspection UI behind an explicit
  development flag).
- **Size secret protection to the actual threat model, not uniformly.** A
  certificate that protects only local, at-rest confidentiality on one
  machine doesn't need the same passphrase discipline as a secret crossing
  an actual trust boundary. Decide per artifact what it protects against.
- **Anything that must survive a restart cannot live only in process
  memory or a per-process temp file.** Audit every "generated fresh at
  startup" artifact (a correlation store, a signing certificate, a key
  ring) for this exact failure mode before calling a design
  restart-safe.
- **Generate secrets and dev certificates with scripts; never commit
  them.** Setup tooling should produce local secrets/certs on demand, and
  your migration's setup docs should name the exact command that produces
  each one.
- **Readiness has two layers, and they fail differently on purpose.** An
  orchestrator-level health check covers the common case. The app's own
  internal readiness poll, for when it runs outside the orchestrator,
  should be **fail-open**: log and start anyway if a dependency never
  reports ready within a deadline — a request that then fails fails no
  worse than without the check, and a host that never starts is worse than
  one that started slow.
- **Bootstrap/provisioning is its own idempotent, one-shot step — never
  folded into app startup, and never assumed to have run.** Make it
  upsert, not blindly insert; poll its own dependency with a bounded retry
  and **exit non-zero** on timeout — the opposite failure mode from the
  app's own readiness gate, because nothing should proceed on
  half-finished provisioning. Declare a hard dependency so starting the
  app always pulls provisioning in too.
- **Order security middleware deliberately and write down why** — the
  order is itself a security property (forwarded-header resolution before
  anything that reads client address or scheme; rate limiting before
  authentication, so an over-budget request never reaches a handler or a
  call to the legacy system).
- **Verify a security-critical framework feature actually behaves as
  documented on your runtime before depending on it.** Trust nothing
  security-critical from a library without a reproduction; hand-roll the
  minimal check if it doesn't hold up, and keep that check small enough to
  audit directly.
- **A multi-step flow gets one shared rate-limit budget, not one per
  endpoint** — splitting a login flow's budget across its steps silently
  doubles it.
- **A circuit breaker (or any stateful resilience primitive) must be a
  long-lived, shared instance per guarded dependency, never constructed
  fresh per call.** Its whole value is state that spans requests; a
  breaker rebuilt on every call can never actually trip.
- **Pin the internal network topology when a security control depends on
  recognizing it**, rather than trusting an orchestrator's dynamic
  default.
- **Bind every exposed port to loopback by default; require explicit
  opt-in to bind wider.**
- **Classify every URL-shaped configuration value as internal
  (service-to-service) or external (browser-facing), explicitly.** This
  bug class — a redirect URI, an issuer string, or a base URL resolved
  against the wrong one of the two — recurs constantly; don't assume "the
  base URL" is single-valued.
- **When two independently deployed units must agree on a value and
  there's no shared-config mechanism, duplicate the computation
  explicitly and document why**, rather than building a shared-config
  system prematurely.
- **Know and document the far side's exact-match semantics before
  assuming "close enough."** A value two systems must agree on needs this
  question answered explicitly: string equality, host match, or
  allow-list prefix?
- **Decide the schema-creation strategy deliberately, and know when to
  graduate off it.** A fast-moving prototype can create its schema
  automatically at startup; know this is a conscious simplification to
  revisit with versioned, reviewed migrations before production.

## 10. Testing strategy

The central difficulty in testing a facade is that the thing it integrates
with — the legacy system — is slow to start, awkward to control, and not
yours to change. That pushes teams toward mocking it entirely, which then
hides the exact bugs a facade is most likely to have (protocol mismatches
and configuration drift between what you registered and what the legacy
system believes).

The resolution is layering: a fast, in-process *fake* that genuinely
speaks the protocol for most tests, plus a small number of tests against
the real legacy system for the things only it can confirm. §10.1 sets out
what each layer can and cannot prove.

### 10.1 The layers, and what each alone can prove

| Layer | What it checks | What it cannot catch |
|---|---|---|
| Architecture tests | The compiled/structural reference graph matches the intended layering | Anything about runtime behavior |
| Unit/in-process tests | Protocol-level correctness against a real-but-fake legacy IdP, in-process, fast and parallelizable | Anything about the actual legacy system's own real-world behavior |
| Black-box integration tests | The real legacy system actually accepts what the facade sends | Only what's exercised — state the suite's boundary explicitly |
| Shadow comparison (pre-cutover) | Legacy and new code paths produce equivalent outcomes for the same scenario | Nothing by itself — it's a gate, not a substitute for the layers above |

**An architectural rule you can state in one sentence should be an
automated test**, not a comment or a code-review checklist item — inspect
the compiled/structural reference graph so a forbidden reference fails the
build the moment it's introduced, not whenever someone happens to exercise
that path.

### 10.2 Testing the legacy-system integration itself

- **Stand up a real, minimal, protocol-speaking fake server for the legacy
  IdP; don't mock the HTTP client.** When the thing under test is protocol
  compliance, fake the protocol — a mocked client interface can't surface
  the protocol-shape bugs a real (if minimal) implementation will.
- **Prove the legacy system accepts your registration by asking the legacy
  system directly**, not just by asserting your own configuration matches
  what you believe you registered.
- **For every resilience threshold, test the negative space as explicitly
  as the positive case.** Don't just prove repeated failures open a
  circuit breaker — also prove an ordinary rejection (a bad credential, a
  normal 404) never contributes toward opening it, however many times it
  happens.
- **Make slow-by-design thresholds fast in tests, through the same
  configuration surface production uses** — not a separate test-only
  bypass.

### 10.3 Testing security-sensitive guarantees

- **Test a "never happens" claim at the most permissive configuration the
  system could ever run at, and guard against a vacuous pass.** If you
  claim a credential or a request body never reaches a log, test it with
  logging opened to its most verbose level, and assert the capture
  mechanism itself produced *something* before asserting the sensitive
  value is absent — otherwise the test can pass forever because its own
  instrumentation silently broke.
- **When a setting is optional-with-a-safe-fallback, test both branches
  with real artifacts**, not just that each one binds — a config-driven
  fork is two code paths; test both, not just the one every other test
  happens to hit by default.

### 10.4 Integration-test readiness fixtures

- **Poll every independent readiness condition concurrently, under one
  shared deadline, with its own per-check timeout** — not a single serial
  chain of waits.
- **Distinguish "this will never pass, stop polling" from "not ready yet,
  keep polling," and fail loudly and specifically on the former** (a bad
  credential vs. a dependency still booting are different failures with
  different remedies).
- **Serialize tests that share one expensive, stateful external resource;
  keep everything else parallel.**

### 10.5 Test-scope discipline

- **State each test layer's actual boundary where a future reader will
  see it** — don't let a green run on a narrowly-scoped suite be read as
  proof of broader coverage.
- **When a feature is removed, decide what happens to its test coverage
  explicitly** — delete the tests that tested it, and name any coverage
  gap that removal leaves behind, rather than letting dead tests linger or
  silently losing coverage.

## 11. Security testing

The attacker's-eye cut through the same test suite — tests that exist
specifically because someone asked "what if a caller lies here?"

### 11.1 Forgery and replay of the identity assertion

- **Reject a correctly signed, correctly issued, correctly audienced token
  that fails on exactly one remaining check — and test that specific
  combination, not just "a bad token."** For every independent validation
  check (issuer, audience, signature, nonce, expiry), the test that
  matters most is "everything else valid, only this one wrong" — it's the
  only version that proves *that specific* check, instead of proving *some*
  check exists.
- **Prove a rejected grant degrades to a generic error, never the
  dependency's own error text** — relevant for information disclosure as
  much as UX.

### 11.2 Trust-boundary and spoofing tests

- **Test the trust boundary from both sides, not just the "it works"
  side.** The same header, the same claimed value, with only the caller's
  network membership changed, should produce two different outcomes. A
  suite that only tests the trusted path can't distinguish "the trust
  check works" from "the header is always honored."
- **Prove the spoofing defense actually changes security-relevant
  behavior** (a cookie's Secure flag, a rate limiter's keying), not just an
  internal flag nothing reads.
- **For any security-relevant setting (a trusted network, an allowed-host
  list, a signing key path), test that bad configuration fails the host
  at startup** — never falls back to an open or permissive default.

### 11.3 Session security and privilege-boundary tests

- **Prove revocation kills the session server-side, from a cookie copy the
  revoking call never touched.** The threat model for revocation is
  specifically "a copy of this credential exists somewhere I don't
  control" — a test that only checks the revoking caller's own next
  request can't distinguish real server-side revocation from a
  client-side no-op.
- **Test blast radius in both directions for any "affects multiple
  sessions" action: it must reach every session it should, and none it
  shouldn't.** Write both halves of this pair for every bulk action the
  facade exposes — under-reach (an attacker's second session surviving a
  revoke-all) and over-reach (one subject's action affecting another's).
- **Test the negative case of a privilege boundary as thoroughly as the
  positive case, and assert the attempt changed nothing** — don't stop at
  asserting a 403; re-check that the target of a rejected privileged
  action is still unaffected.
- **Test the self-targeting edge case of any admin/elevated action
  separately** — "can affect anyone" and "can affect anyone, including
  themselves" are easy to get different answers to by accident.
- **Every session-mutating or identity-revealing endpoint needs its own
  "no session, no access" test** — middleware wiring regresses per-route
  often enough that each sensitive route deserves its own assertion, even
  when several routes share the same underlying authorization check.

### 11.4 Abuse and rate-limit boundary tests

- **Test the exact boundary (N passes, N+1 is refused)**, not "a lot of
  requests get refused eventually."
- **Test that a shared budget across a multi-step flow can't be doubled
  by alternating between its steps.**
- **Test that the limiter does *not* apply somewhere it shouldn't**, as
  explicitly as proving it applies where it should — guard against a
  policy accidentally applied too broadly, silently breaking a legitimate
  high-frequency caller.
- **Apply the "bad config fails startup" rule to rate-limit settings
  too** — close off degenerate configurations that would lock out every
  caller or rate-limit nothing at all.

### 11.5 Input-boundary tests: audit for the gap, don't assume coverage

Periodically grep for every security-relevant validator/guard in the
codebase (an open-redirect allow-list, a host/scheme check, an input-length
guard) and confirm each one has a test that actually drives its *rejection*
path. "The code is short and looks obviously correct" is not evidence that
it's tested — this is exactly the kind of gap that stays invisible until
someone deliberately looks for it.

### 11.6 Fail-closed configuration, as a cross-cutting test category

Any option controlling a security decision (a trusted network, a rate
limit, an admin allowlist, a certificate path) should have a startup test
proving invalid or degenerate values refuse to start the host — not just
tests proving valid values work. Add this test in the same change that
introduces the setting, not as follow-up.

## 12. Logging and performance monitoring

Two different needs get conflated here, so it's worth separating them up
front. An **audit trail** answers "who did what, when" after the fact, for
security and compliance — it must be complete, trustworthy, and free of
anything sensitive. **Monitoring** answers "is this healthy right now" —
it needs numbers over time, not individual records. A facade needs both,
and structured logs alone give you only the first.

### 12.1 Structured, stable-category audit logging

- **Give the audit trail its own fixed, stable logger category,
  independent of which file or module a logging call happens to live
  in** — a refactor that moves a call shouldn't silently rename the
  category a log pipeline or alert is filtering on.
- **Use structured, strongly typed logging for anything queried or
  alerted on**, not ad hoc string interpolation — named, typed fields that
  a log backend can filter and aggregate on directly.
- **Scope the audit trail to where data actually moves, and say why
  something in scope was deliberately left out.** A high-volume,
  low-sensitivity endpoint (an identity check that returns no sensitive
  data, fired on every page load) can be a deliberate, stated exclusion —
  a real tradeoff between volume and signal, made explicitly rather than
  either logging everything or logging arbitrarily.
- **Never log a reason string that could leak a dependency's own error
  detail.** Use a short, fixed internal label instead — the same
  discipline applied to what reaches the log as to what reaches the
  client.
- **Redaction is a tested guarantee (§10.3), not just a logging-policy
  statement** — design and test are two different artifacts; a facade
  needs both.

### 12.2 Liveness vs. readiness: know which one you actually have

- **A health endpoint that always returns success answers "is the process
  up," nothing about whether it can actually serve a request** (reach the
  legacy system, reach its own session store). Know, for every health/probe
  endpoint in the system, which of the two questions it actually answers —
  don't let an orchestrator's liveness probe quietly stand in for a
  readiness probe, or a load balancer keeps sending real traffic to a
  process that's "up" but can't serve it.
- A production deployment needs a readiness endpoint that actually checks
  its own critical dependencies (at minimum, its session store) — don't
  assume a `/health`-named endpoint implies that coverage.

### 12.3 What a facade's observability needs beyond structured logs

Structured logs and a liveness check are not the same thing as the
observability a real production deployment needs. Add, deliberately:

- **Latency and error-rate metrics per outbound dependency call**,
  broken out individually (the token exchange, a key/JWKS fetch, each
  proxied call) — a circuit breaker trips on a ratio you can't tune, or
  notice drifting, without the metric underneath it.
- **Circuit-breaker state transitions as a first-class signal.** Alert on
  *open* (something is actually wrong with the legacy system); log/metric
  *half-open* probe outcomes. The facade's own resilience layer is one of
  the highest-signal indicators of legacy-system health available, and
  it's wasted if only the breaker itself knows it tripped.
- **Rate-limit rejection counts as both a security and a capacity
  signal**, fed somewhere alerting can see it — a spike is either an
  attack or a legitimate pattern the configured limit no longer fits, and
  the metric is what tells you which.
- **Session-store latency**, once a server-side session store exists — it
  sits on the critical path of every authenticated request, not just
  login.
- **Correlation/trace IDs propagated across every hop** (gateway → facade
  → legacy system, and back), so a slow or failing request can actually be
  followed across the boundary the facade exists to bridge — otherwise
  every incident investigation starts by manually correlating timestamps
  across separately formatted logs.

## 13. CI/CD

An auth facade is unusually sensitive to *configuration* rather than just
code — which means a CI pipeline that builds and unit-tests the code, but
runs it in a configuration nobody will ever deploy, misses the most likely
class of failure. The theme of this section: make CI run the real thing,
configured the real way.

- **Test what people actually run.** Build CI's configuration from the
  same template a developer or deployment uses, not a bespoke, looser
  CI-only configuration — a CI pipeline more permissive than production,
  by convenience, is testing a system that doesn't exist. Run the real
  orchestrated multi-service stack in CI, not a stubbed-down subset.
- **Secrets in the pipeline:** fall back to a known-fake secret when a
  real one isn't configured, drawing from the CI secret store first, so a
  run without secrets configured still gets a deterministic, throwaway
  credential rather than an empty value that fails some other way
  downstream. Never let the pipeline echo a secret value, even for
  debugging — print variable names, never values. A CI log is often a
  lower-trust-tier artifact than the secret store feeding it.
- **Generated-artifact drift as its own gate.** Anything committed but
  generated (an API client, a schema snapshot) gets a CI step that
  regenerates it and diffs against the commit — not just trust in whoever
  ran the generator last. Order that check correctly relative to the
  stack's own startup dependencies; a regeneration step needs the same
  readiness the app itself needs.
- **Reuse the orchestrator's own health checks as the CI wait condition**,
  instead of reinventing polling in pipeline script — one readiness
  definition serves local dev and CI alike.
- **Give a slow-starting dependency a generous, explicit wait timeout,
  bounded by an overall job timeout** — slow-but-working first boots
  aren't penalized, but a truly hung run still gets killed.
- **Add a dedicated, best-effort diagnostics step that runs even when
  earlier steps failed**, separate from the actual test assertions —
  diagnostics and assertions are different steps with different
  conditions, not bolted together.
- **Tear the stack down unconditionally, including its volumes/state** —
  a failed run shouldn't leave stale state for the next one.
- **Pull sanitized fixture data by a pinned, versioned reference, never
  commit it into the repository** — deterministic and reproducible per
  run, without bloating the repository, and the version itself documents
  exactly what the fixture was captured against.
- **Re-apply fresh, run-specific credentials onto a restored static
  fixture**, rather than baking credentials into the fixture itself — so
  the same fixture artifact is reusable indefinitely without ever needing
  its own secret rotated.
- **Pin the toolchain version from one file both CI and local dev read** —
  avoid the class of bug where CI silently uses a different compiler/SDK
  version than a developer's machine resolves locally.
- **Grant the pipeline's credentials the minimum scope they need.**
- **Cancel superseded runs instead of letting them queue and consume
  capacity** — especially for a pipeline this expensive (standing up a
  full multi-container stack per run).
- **Upload structured test results as a build artifact, not just console
  output** — a partial or failed run should still leave an inspectable,
  durable record.

## 14. Containers

Nothing in this section is specific to authentication, but a facade is a
security-sensitive, internet-facing component that talks to a legacy
system you often cannot modify — which makes it exactly the place where
sloppy container defaults are most costly. If you're already fluent in
container hardening, skim for §14.4 (dependency conditions) and §14.5
(root), which are the two most commonly gotten wrong in this specific
shape of system.

### 14.1 Dockerfile/image-level choices

- **Multi-stage builds: a full toolchain to compile, a minimal runtime to
  ship.** Ship the smallest image that can actually run the artifact, not
  the image that built it — a compiled static-asset frontend may need no
  runtime at all beyond a plain web server.
- **Order build steps for layer-cache reuse**: copy manifest/lockfiles and
  install dependencies before copying application source, in any
  language — a source change then invalidates only the final layer, not
  the (slow) dependency-restore layer.
- **Run as the image's non-root user by default; know why before
  overriding it.** This is exactly the kind of component where a
  container escape matters most.
- **A port-exposure declaration documents intent; it doesn't create a
  listener** — the real bind configuration lives in whatever
  environment/code actually binds it.
- **Pin runtime/base images by digest, not just by tag, and state any
  deliberate exception explicitly** (a fixture-coupled legacy image pinned
  by version tag because the fixture itself is version-coupled, for
  example).
- **Mount read-only wherever the container only needs to read** (secrets,
  provisioning scripts) — a compromised container can't use that foothold
  to modify the secret material or the script that configured it.
- **Choose a restart policy per container role, not uniformly** —
  long-running services restart automatically; one-shot provisioning
  containers should not, or a real failure gets masked as an endless
  crash-restart loop.
- **State plainly when local/CI credentials are deliberately
  known/shared**, rather than letting that fact hide in an example config
  file, and compensate with network defaults (loopback-only binding)
  rather than secrecy — the right call for a disposable stack, and a
  decision that must flip the moment the stack stops being disposable.
- **A health check should probe actual readiness, not just that the
  process started.**

### 14.2 Container networking: two addresses for every service

An orchestrator's embedded DNS resolves service names for
container-to-container traffic; published ports are for the host (or a
browser) only — and a facade touches both simultaneously (it calls the
legacy system internally by its service name, while a browser must reach
the facade itself at a published, host-reachable address). Know, for every
container-to-container call, whether it's an internal hop or something an
external caller must resolve — the two are never interchangeable, and
getting this backwards is a recurring bug class in exactly this kind of
system.

Pin the orchestrator's network topology (a fixed subnet) when any
container-level logic parses an incoming address against it — a
trusted-proxy check is only meaningful if the network it checks against is
stable.

### 14.3 Volume types: match the mount to what the data actually needs

- **Named/managed volumes** for state the container itself owns and
  mutates (a database's files, a legacy system's own writable site data) —
  must outlive container recreation, has no reason to be visible as plain
  host files.
- **Read-only bind mounts** for anything the host hands the container as
  input (generated certificates, provisioning scripts) — the running
  container only ever needs to read it, and a compromised container
  gaining write access to its own provisioning script would be a
  privilege-escalation path for nothing it legitimately needs.

Getting this backwards — a bind-mounted database directory, or a writable
mount for a provisioning script — either loses durability or widens what a
compromised container can modify.

### 14.4 Dependency conditions are not one thing — pick deliberately

An orchestrator's "depends on" relationship usually comes in (at least)
three flavors, each for a different reason:

- **Wait for the dependency's own health check**, not just its process
  starting — for any dependent that would fail outright against a
  dependency that's merely "running" but not yet actually serving.
- **Wait for a one-shot container to finish**, not to become healthy (it
  has no long-running health to check) — the mechanism behind "bootstrap
  is its own idempotent step, never assumed to have run" (§9): the
  orchestrator itself refuses to start the dependent until provisioning
  has actually completed, rather than the ordering being a documentation
  promise someone could run out of sequence.
- **Start-order-only, no wait** — appropriate only when the dependent can
  tolerate the dependency not being ready yet (because its own retry/
  circuit-breaker logic already covers that gap). Using this form where a
  real readiness dependency exists is the mistake to watch for: it often
  looks identical in configuration to the health-gated form, differing by
  one easy-to-miss keyword.

### 14.5 Root considerations, in more depth

- **What a built-in non-root user actually is.** Many official base
  runtime images ship a built-in, unprivileged user baked into the image
  itself — using it needs no extra setup, just switching to it in the
  *runtime* stage of a multi-stage build. The build stage above it running
  as root is normal and low-risk, since nothing from that stage ships.
- **Avoiding a privileged port (<1024) is what makes non-root possible
  without granting a capability back.** A non-root process can't normally
  bind port 80/443 without an elevated capability. Listening on a high
  port inside the container, and letting the orchestrator's own
  port-publishing do the 80/443-shaped remapping at the host level, avoids
  needing that capability at all. Know this tradeoff explicitly before
  assuming a service "needs" a privileged port — it almost never does,
  once the remapping happens outside the container instead of inside it.
- **Read-only bind mounts sidestep the usual non-root permission
  friction.** A non-root process writing to a bind-mounted host directory
  commonly hits a UID/GID mismatch; making every bind mount read-only
  avoids the problem entirely rather than solving it, because nothing
  mounted that way is ever written to from inside a container.
- **Non-root discipline applies to images you build; it does not reach
  into images you only consume.** A "we run non-root" claim needs to
  specify which containers it actually covers — it's a per-image property,
  not a property of a stack as a whole, and a legacy system's own vendored
  image is usually the one container with the least control available
  over it.
- **A one-shot provisioning container is a different risk profile than a
  long-running root process**, even if both technically run as root — a
  narrower, time-boxed exposure window than a web-facing process that
  stays up and keeps accepting requests as root for the life of the
  container. Don't apply the same scrutiny uniformly.
- **Further hardening beyond "non-root":** dropping Linux capabilities
  (adding back only what's actually needed), disallowing privilege
  escalation, and running with a read-only root filesystem (with explicit
  writable mounts only for the few paths that need it) are each a real
  step past "don't run as root" worth evaluating per service before
  production — a non-root process can still do damage with an unneeded
  capability or a writable filesystem it doesn't need.

### 14.6 Gaps worth naming explicitly

Following §11.5's own practice — name an absence plainly instead of
implying full coverage:

- **Resource limits** (CPU/memory) are easy to leave unset entirely in a
  local/CI stack. Reasonable as a starting simplification; not acceptable
  at the production bar (§15) — set them from an actual load test, don't
  inherit "no limit" as if it were a tested default.
- **Container image vulnerability scanning** is easy to omit from a CI
  pipeline that otherwise builds and runs every image. A short, addable
  step (an image-scanning tool in your CI provider's ecosystem) closes
  this; name it as a gap if it isn't there yet, rather than letting a
  pipeline that merely builds images be mistaken for one that also
  verifies them.

## 15. Production readiness checklist

The migration checklist (§8) gates *building* the facade. This one gates
*exposing it to real users and real data* — a separate, later bar. Treat
every unchecked item as an explicit accepted risk (§7), not a silent gap,
until it's checked.

**Secrets and key management**
- [ ] Every secret comes from a real secrets manager with access control
      and rotation — not a plain config file with known/shared credentials.
- [ ] Encryption/signing certificates are CA-issued or otherwise
      trust-chain-valid, not self-signed dev certificates, and have a
      documented, no-downtime rotation procedure.
- [ ] Any session/state key ring has a rotation plan that doesn't
      invalidate every live session at once.
- [ ] Per-environment separation is real: distinct identity-provider
      config, database/storage credentials, encryption/signing keys, and
      observability destinations, with no lower-trust environment able to
      reach a higher-trust one.

**Scaling and availability**
- [ ] Multi-instance operation is *proven*, not just unblocked — closing
      the structural blockers is a precondition, not the same claim as
      having run two replicas under load and through a rolling deploy.
- [ ] A rolling deploy mid-login and mid-session has been tested, not
      just a clean restart.
- [ ] Resource limits and capacity targets are set from an actual load
      test, not left at defaults.
- [ ] Rate-limiter keying has been re-validated against the real
      production network path — a limiter keyed on client address behaves
      differently with more than one hop or load balancer in front of it.

**Resilience and dependency on the legacy system**
- [ ] Every known upstream legacy-system defect affecting auth is either
      fixed upstream, has a compensating control, or is a formally
      signed-off accepted risk — not silently unguarded.
- [ ] Circuit breakers, rate limits, and timeouts are tuned from real
      traffic/latency data, not conservative defaults chosen with no real
      load.
- [ ] A degraded-legacy-system runbook exists: what a human does when a
      circuit breaker trips, who gets paged, what users see.

**Authorization**
- [ ] A real authorization model exists for what an authenticated
      identity may do with any system's data — "the facade confirms
      identity only" is an accepted risk at prototype stage, not a
      production posture.
- [ ] Admin-level actions (session revocation, impersonation, support
      tooling) are gated by a real role/claim, not a fixed config-file
      allowlist.
- [ ] OAuth/OIDC scopes requested are exactly what's needed for what's
      actually built.

**Observability and incident response**
- [ ] Metrics and distributed tracing exist across the facade and the
      legacy-system boundary, not just an audit log.
- [ ] Audit log coverage, retention, and redaction match your actual
      compliance regime — logs redact tokens, credentials, and sensitive
      content by default.
- [ ] Alerting exists for the security-relevant signals that matter:
      spikes in failed logins, circuit-breaker trips, rate-limit
      rejections, session-revocation calls.
- [ ] An incident runbook exists for a suspected compromised session or
      credential: who can revoke it, how fast, and what gets checked
      afterward.
- [ ] Backup and restore is tested for whatever now holds session/identity
      state, including a timed restore-from-backup drill.

**Supply chain and build**
- [ ] Every base/runtime image is pinned by digest, with a documented,
      deliberate exception policy where it isn't. Dependency/image
      scanning runs in CI.
- [ ] Schema management has moved from an automatic-create convenience to
      versioned, reviewed migrations with a tested rollback path.

**Compliance and sign-off**
- [ ] Whatever regulatory framework governs the data this system touches
      has been reviewed against the actual design — not assumed satisfied
      because credentials are delegated to a legacy system that was
      itself compliant.
- [ ] A security review/penetration test has been performed against the
      *production* configuration specifically — a review of a local/dev
      stack is necessary but not sufficient.
- [ ] The vulnerability-disclosure and triage process is live and
      staffed before production traffic exists, not written and left
      dormant.
- [ ] A rollback plan exists: if the facade must be pulled from
      production, what happens to active sessions, in-flight logins, and
      the legacy system's own OAuth client registration.

## Putting this into practice

("How to read this document" is near the top; this is how to *apply* it
once you've read it.)

1. Start with §1–§2 and get real answers from real stakeholders before
   designing anything.
2. Walk the decision graphs in §3 to settle the big architectural forks
   early; revisit §3.4 specifically at whatever milestone first makes a
   dedicated identity service plausible.
3. Build in the five-stage order from "The core strategy": stand-in → real
   flow → BFF → retire the stand-in → harden → prove scale. Don't skip
   straight to "harden" before the real flow exists, and don't skip
   deleting the stand-in once it's unused.
4. Use §4 (hardening), §9 (setup/config), §10–§11 (testing and security
   testing), §12 (logging/monitoring), §13 (CI/CD), and §14 (containers) as
   working checklists while building, not as a retrospective audit.
5. Keep the accepted-risk ledger (§7) alive continuously; gate production
   exposure on §15, not on §8 alone.
