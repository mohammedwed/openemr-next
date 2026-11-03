# Handbook: migrating authentication off a legacy system

One self-contained framework for putting a modern login in front of an old
system that already owns your user accounts — the decisions, the build
order, the hardening, the testing, the pipeline, and the gate before real
users.

This handbook cites nothing. Where a decision matters, the reasoning is
written out here rather than pointed at: there is no index to consult, no
decision record to go and read, no file in some other repository that
holds the real answer. Everything needed to make and defend each call is
in this document.

It is also deliberately generic. No vendor, language, framework, or
codebase is assumed. Concrete examples appear throughout, drawn from one
real migration of a clinical-records system, but they appear as worked
illustrations — "here is what actually went wrong, and why" — not as
references you need access to.

## What this covers, and what it does not

**In scope:** the facade/bridge service itself. Delegated credential
verification, the browser login flow, session and token handling, the
server-side proxy that lets a frontend reach legacy data, and the
hardening, testing, observability, pipeline, and container concerns around
that one component.

**Explicitly out of scope**, and not solved anywhere below:

- **Authorization** — what a signed-in identity is *allowed to do* with
  legacy or new-service data. The facade answers "who is this." It does
  not answer "what may they do," and nothing in this handbook should be
  read as providing that. It is a separate workstream, called out again at
  every point where it would be tempting to assume otherwise.
- **User/account migration** — moving, merging, or deduplicating accounts
  into a new system of record. The facade here never becomes the account
  store. If yours does, that is a deliberate fork (§9.4) with its own
  migration plan, not a drift.
- **Identity-lifecycle flows** the legacy system already owns — multi-factor
  enrolment, password reset, account recovery. If the legacy system
  provides them, the user meets them on the legacy system's own login
  page, and the facade never sees them. If it doesn't provide them, adding
  them means becoming an identity provider, which is §9.4's fork.
- **Broader service decomposition** — carving a monolith into bounded
  contexts, per-service databases, anti-corruption layers, event
  publishing. Touched here only where it intersects with identity (§22).

## How to read this

**New to this problem.** Read Part I end to end, then stop. It is the
orientation: the problem, the shape of the answer, the vocabulary, and the
five stages a facade goes through. Come back for Part II when you are
ready to design, and Part III when you are ready to build.

**Designing one now.** Part II is the whole job: the questions to ask
before anything is decided, the four decisions that are expensive to
reverse, the tradeoffs, and the risk ledger that keeps the deferred
decisions from going quiet.

**Building one now.** Part III is a sequential build order — ten phases,
each step with a verifiable finish line and the specific mistakes that
happen at that step. Work it in order. Keep Part IV open beside it as a
set of working checklists.

**Inheriting one.** Read §14 (hardening requirements) and §24 (production
readiness) as an audit. They are the two lists that tell you what should
already be true of a facade someone else built.

**Reviewing one.** §9 (decision graphs), §10 (tradeoffs), §11 (the risk
ledger), and §24 (production gate) are where the defensible-or-not
judgments live.

---

# Part I — Orientation

## 1. The problem, in plain terms

You have an old system. Call it **the legacy system**. It holds the real
user accounts and passwords, and it is the only thing that can truthfully
answer *"is this person who they say they are?"*

You now want to build something new next to it — a new web app, a new API,
eventually several new services — and those new things need to know who the
user is.

You have three options, and only one of them is usually a good idea.

**Option 1: copy the user accounts into a new system.** Now two systems
both believe they own the truth about identity, and they will drift apart.
Every account change has to be replicated, every password change
synchronised, every deactivation mirrored — and the first time they
disagree, you have an incident with no correct answer. Expensive, risky,
and rarely necessary at the start.

**Option 2: let every new app talk to the legacy system's login
directly.** Now every new app has to understand the legacy system's
quirks: its undocumented logout contract, its exact-match redirect
comparison, its habit of returning a server error instead of a clean
rejection on some bad passwords. Each quirk gets reimplemented — slightly
differently — in each app. Fixing one does not fix the others.

**Option 3: build one small piece in the middle.** It is the only thing
that speaks the legacy system's login language. Everything new talks to
*it* instead, in a modern, standard way. This handbook calls that piece a
**facade** (also "bridge", or "BFF" — backend-for-frontend).

This handbook is about option 3.

## 2. The shape of what you are building

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

Two rules make this work, and most of this handbook is an elaboration of
one or the other:

- **The facade never stores passwords or user accounts.** It asks the
  legacy system, every time. It is a translator, not a second source of
  truth.
- **The browser gets a session, not a key to the kingdom.** The facade
  keeps anything genuinely powerful — the legacy system's own access and
  refresh tokens — on the server side, and hands the browser only a
  meaningless reference to its session.

If you remember nothing else from this document, remember those two.

## 3. Terms used in this handbook

Skim once; come back when a term trips you up. Nothing here needs
memorising, and no prior OAuth/OIDC expertise is assumed.

| Term | What it means, plainly |
|---|---|
| **Legacy system** | The old system that owns the real user accounts and passwords. Assumed unchanged throughout. |
| **Facade** (bridge, BFF) | The small new component in the middle. The only thing that speaks the legacy system's login language. "BFF" = backend-for-frontend: a backend that exists specifically to serve one frontend. |
| **Identity provider (IdP)** | Whatever actually verifies credentials and issues proof of identity. Here that is the legacy system — *not* your facade. |
| **OAuth2 / OIDC** | The industry-standard protocols for "let this app know who the user is, without giving it their password." OIDC (OpenID Connect) is a layer on top of OAuth2 that adds identity. If the legacy system already speaks these, your job is much easier. |
| **Authorization-code flow** | The standard, safe login dance: the app sends the user to the IdP's own login page, the user logs in *there*, and the IdP sends the app back a short-lived one-time **code**, which the app exchanges server-to-server for tokens. The user's password never touches your app. |
| **PKCE** | ("pixy") An add-on to the authorization-code flow that stops an attacker who intercepts the code from being able to use it. Required for apps that cannot keep a secret (browsers, mobile). |
| **Credential/password grant** | The old, discouraged shortcut: your app collects the user's password and sends it to the IdP itself. Simple to build, which is why it shows up as a temporary stand-in — and why this handbook keeps telling you to delete it. |
| **Access token** | A short-lived string that proves "the bearer of this is allowed to call this API as this user." Powerful; treat like a password. |
| **Refresh token** | A longer-lived string used to get a new access token without making the user log in again. Even more sensitive than an access token. |
| **Bearer token** | Any token where simply *holding* it is sufficient to use it — no further proof required. Hence: never give one to a browser if you can avoid it. |
| **Session cookie** | A small value stored by the browser and sent on every request, identifying the user's logged-in session. |
| **Opaque session reference** | A session cookie whose contents mean *nothing* on their own — just a lookup key into a record on your server. Preferred, because you can delete the server record to kill the session instantly. |
| **Self-contained (stateless) session** | The opposite: a cookie that carries the actual encrypted session data inside it. No database needed, but you cannot revoke it — the browser holds a valid session until it expires on its own. |
| **Revocation** | Deliberately ending a session or token before it expires naturally (because a laptop was stolen, say). |
| **Scope** | The specific permissions an app asks for when requesting a token ("read the user's name", "call the patient API"). Ask for the least you need. |
| **Claim** | A single fact inside a token ("the user's ID is 42", "this token expires at 3pm"). |
| **Rate limiting** | Refusing requests from the same source beyond N per time window, so nobody can hammer your login endpoint. |
| **Circuit breaker** | A wrapper around calls to a dependency that, after enough failures, stops trying for a while and fails immediately instead. Prevents one sick dependency from hanging every request in your system. |
| **Reverse proxy / gateway** | A component that receives requests and forwards them to the right backend. Used here to give everything one shared public address. |
| **Forwarded headers** | When a proxy sits in front of your app, it tells the app the *original* client's address and protocol via headers like `X-Forwarded-For`. These are trivially forged, so they must only be trusted from the proxy itself. |
| **Key ring** | The set of cryptographic keys your app uses to encrypt and sign things like session cookies. If it is regenerated each time the app restarts, every existing session breaks. |
| **Liveness vs. readiness** | "Is the process running?" vs. "can it actually serve a request right now?" Different questions, often wrongly treated as one. |
| **Idempotent** | Safe to run more than once with the same result. Important for setup and provisioning scripts. |
| **Stand-in** | Any deliberately temporary implementation built to unblock progress, intending to delete it. The password grant is the classic one here. |

## 4. The core strategy: delegate, don't duplicate

A legacy system already owns identity — real accounts, real credential
checks, often a real OAuth/OIDC server already sitting there unused. The
temptation is to build a new, independent identity system "properly" and
migrate users into it. The far cheaper and lower-risk strategy, and the one
this handbook assumes throughout:

**Build a thin facade that verifies identity by asking the legacy system,
never by holding a second, independently-correct copy of credentials or
accounts.** The facade's job is to translate the legacy system's own
authentication into something a modern client can safely consume. Nothing
more.

This unfolds in five stages. The order matters, and each stage is a
deliberate decision rather than a default to drift into.

**Stage 1 — Build a stand-in, and label it temporary on day one.**
Early on you need *something* working so the rest of the team is not
blocked. The fastest path is usually the discouraged one: collect the
password yourself and hand it to the legacy system (the password grant).
That is acceptable as scaffolding.
*Why it is a trap:* scaffolding that works tends to become permanent.
*What to do:* write down, in the same change that adds it, the condition
under which you will delete it — "when the real browser flow works end to
end through the gateway." A removal criterion written later never gets
written at all, because by then the stand-in works and nobody wants to
touch it.

**Stage 2 — Build the real login flow.**
Switch to the authorization-code flow, with PKCE, against the legacy
system's own login page. The user types their password into the legacy
system, not into your app.
*Why:* your facade never sees, stores, logs, or accidentally leaks a
password it never received in the first place. This single change removes
a whole category of risk rather than mitigating it — and it removes the
need for most of the discipline in §15 around handling plaintext.

**Stage 3 — Keep the powerful tokens on the server.**
Once login works, the facade holds real access and refresh tokens for the
user. Do not pass them to the browser. Give the browser an opaque session
reference instead, and have the facade attach the real token server-side
when it needs to call the legacy system on the user's behalf.
*Why:* a token in a browser can be stolen by any script that runs on your
page, and a stolen token usually cannot be revoked. A stolen session
reference can be (stage 5).

**Stage 4 — Delete the stand-in from stage 1.**
Once nothing calls the old path, remove it: the endpoint, its
configuration, its tests, and any code that existed only to support it.
*Why:* an unused login path is still a login path. It is attack surface
nobody monitors, maintained by nobody, understood by nobody after six
months. "We might need it for mobile someday" is not a reason to keep it —
mobile needs the stage 2 flow too, launched in a system browser.

**Stage 5 — Harden, then prove it scales.**
Now add what makes it operationally real: revocation, resilience when the
legacy system is down, an audit trail, and keys and certificates that
survive a restart. *Only then* test running more than one copy.
*Why that order:* almost everything built in stage 3 quietly assumes a
single process. Discovering that while also debugging a production scaling
incident is considerably worse than discovering it deliberately, on a
Tuesday, with two containers and a load balancer you set up on purpose.

## 5. How one migration actually went

This is the history the rest of the handbook generalises from — one real
migration, over roughly three months, of a facade in front of a
clinical-records system. It is included because the *sequence* is the
lesson, and because almost every stage below was a correction of the stage
before it.

**It started as a token broker.** Nothing existed, the frontend team was
blocked, so the first version was a token endpoint accepting a username and
password, forwarding them to the legacy system's password grant, and
issuing *its own* freshly minted token on success. This worked in a week.

**It stopped minting its own tokens.** Issuing its own token meant the
facade now owned token semantics, expiry, and signing — a second source of
truth about identity, exactly what the strategy in §4 says to avoid. The
correction was to return the legacy system's own token response unchanged
instead. Less code, one authority.

**It moved to the real browser flow.** The password grant meant credentials
passed through the facade on every login. Switching to authorization-code
with PKCE, against the legacy system's own OIDC server, meant they never
did again. This was the single largest risk reduction in the whole
migration, and it also surfaced most of the legacy system's real quirks —
its exact-match redirect comparison, its undocumented logout contract —
because for the first time the facade was using the legacy system's
protocol rather than its shortcut.

**It became a BFF rather than a broker.** Passing the legacy token back
through to the browser was still handing a bearer credential to a
JavaScript environment. The correction: a session cookie only, with the
real token held server-side in the session, and a server-side proxy that
attaches the token on the one hop that actually needs it.

**It deleted the stand-in.** The password grant had been unused for weeks
by this point. Removing it took out the endpoint, the bearer authentication
scheme that existed only to validate the tokens it issued, and three of
five architectural layers that existed only to serve it. The removal
criterion had been written down at the very beginning — three weeks before
it was met — and that is why it actually happened. Keeping the grant "in
case a mobile or machine-to-machine client needs it later" was explicitly
considered and rejected: a real future client gets its own decision against
what the legacy system supports *then*, not a standing justification for
leaving unused auth surface live today.

**It was hardened.** A session cookie with no server-side record cannot be
revoked, so the session moved into a dedicated database of its own, with
the cookie reduced to an opaque reference. Revocation became deleting a
row. Then: silent token refresh, admin-initiated revocation, an audit
trail, persisted signing and encryption certificates (they had been
regenerated on every restart, silently logging everyone out), and a startup
gate that refuses traffic until the legacy system and the provisioning
steps are confirmed ready.

**It got a capstone.** Twenty-odd scattered decisions were consolidated
into one document stating the finished design's guarantees end to end —
and, next to them, the risks deliberately *not* addressed. That second list
turned out to be the more valuable half, and §11 is about why.

The throughline worth extracting: **the stand-in that unblocks early work
is explicitly temporary, named as such at the start, and actually deleted
once the real flow exists** — not left running in parallel "just in case."
Every other correction in that list was cheap. The one that would have been
expensive is the one that was prevented by writing a single sentence down
at the beginning.

---

# Part II — The decisions

## 6. Questions to ask stakeholders

Answer these before any architecture discussion. In the migration described
in §5, essentially all of the rework — six architectural layers built and
then deleted — traces back to answering the "do we actually need this"
questions late instead of first.

These need a *stakeholder*, not your team. Get the answers in writing.

**Scope and ownership**

- Who today can answer "is this username and password combination
  correct"? Is there more than one such system — a second identity
  provider for a subset of users, a directory service for staff, an older
  instance nobody has decommissioned?
- Does the legacy system expose a standards-based protocol (OAuth2/OIDC,
  SAML) already, or only a proprietary login form and credential table?
  This single answer determines whether you delegate to a protocol or have
  to build a verification adapter yourself (§9.1).
- Who owns the user and credential data after the migration — does it move,
  get duplicated, or stay exactly where it is, permanently? **"Stays put
  permanently" is a valid, durable answer**, and usually the right one. It
  is worth saying out loud, because teams often assume a migration *must*
  end with the data somewhere new.
- What compliance or audit regime constrains where credentials or tokens
  may be logged, stored, or transit? This shapes the audit design (§18)
  and the never-log-a-credential requirement that runs through the whole
  handbook.

**Client population**

- Which clients need to authenticate *today*: browser, native mobile,
  server-to-server, third-party integrations? Get this in writing, and
  **do not build for a client that does not exist yet.** The migration in
  §5 explicitly rejected keeping a password grant for hypothetical mobile
  and machine-to-machine clients, on the grounds that a real one would get
  a real decision against real requirements later.
- For each client type: does it need the legacy system's own token at all,
  or only an authenticated session with the new frontend? Most
  browser-facing answers are "only a session," which is what makes the BFF
  pattern the default.

**Operational constraints**

- Will this run as one instance, or must it scale horizontally and survive
  rolling deploys from day one? This changes the session-storage answer
  entirely (§9.2), and it is much cheaper to answer now than to retrofit.
- What is acceptable behaviour when the legacy system is slow or down: fail
  closed, fail open, or degrade one specific feature? This is the input to
  the circuit-breaker design (§14, Phase 7.2) — a breaker needs to know
  what "failing" should look like to a user.
- What is the actual credential policy already in production? **Do not
  assume a clean policy.** Existing accounts may predate any policy that
  exists today, and a facade that enforces a modern policy at login will
  lock out real users whose passwords the legacy system would have happily
  accepted. Your facade must never reject a credential the legacy system
  would accept.
- Is there a path to revoke a compromised session *today*, without legacy
  system cooperation? Who is allowed to revoke someone else's session, and
  how is that authorisation established? If the answer is "nobody can," you
  have found a requirement, not a gap in your knowledge.

**Timeline pressure**

- Is there a deadline that justifies a known-temporary stand-in — a grant
  type you intend to delete later? If yes, **write the deletion criterion
  down now, before building the stand-in.** In §5's migration the removal
  condition was written three weeks before it was met, and that sentence
  is the reason the stand-in was actually removed rather than quietly
  inherited.

## 7. Architectural questions to resolve with your team

These are your team's to answer, not a stakeholder's. None has a
universally right answer, but each has a sensible default, given below.
Answering them late is what produces expensive rework: several are cheap
now and very expensive once you have real users and real sessions.

### Identity ownership

- Does the facade **verify** credentials (delegate to the legacy system) or
  **issue its own** identity assertions? Delegating is almost always right,
  and it held for the entire migration in §5 without ever being revisited.
- Is the facade a **token broker** (hands a token to any caller), a **BFF**
  (holds the token server-side, gives browsers a cookie only), or a
  **dedicated identity service** (becomes the system of record itself)?
  Default to BFF. Treat "dedicated identity service" as a deliberate, later
  fork (§9.4), never a starting assumption — §5's migration went
  broker → BFF and left the permanent question open behind a milestone
  gate, which is a reasonable way to handle it.

### Session model

- Where does session state actually live: a self-decrypting cookie
  (stateless, cannot be revoked without a separate blocklist) or a
  server-side record referenced by an opaque cookie key (stateful,
  revocable)? Start stateless only if you can state, in writing, that
  revocation is not a requirement yet. §5's migration started stateless
  implicitly — not as a decision, just as what the framework did by
  default — and moved to server-side the moment revocation became a real
  requirement.
- Does the browser ever hold a real, legacy-recognised token, or only an
  opaque session reference? **This is the single biggest blast-radius
  decision in the whole design.** Default to never. The facade in §5
  settled on an absolute: the browser is never, under any code path, handed
  a bearer token.

### Token and credential handling

- If credentials must pass through the facade at all, what is the
  retrieved-once-and-cleared discipline, and is it tested? Prefer avoiding
  the question entirely via a redirect-based flow where the facade never
  sees the password.
- Does the legacy system's token refresh transparently to the client, or
  must the client notice and re-authenticate? Prefer silent, server-side
  refresh with a safe fallback: if the refresh fails, serve the stale token
  and let the legacy system's own rejection decide the outcome rather than
  inventing your own failure. That fallback is what keeps refresh invisible
  to the browser even when it goes wrong.

### Multi-instance and deployment

- Everything "per-process" today is a hidden single-instance assumption.
  There are usually exactly three, and they are easy to miss because each
  works perfectly with one replica: **the protocol library's interim state
  store** (where it keeps the correlation data between the redirect out and
  the redirect back — often a local file), **the encryption and signing
  certificates** (often generated fresh at startup), and **the session
  cookie's own key ring** (often held in memory). Audit for all three
  before scaling past one replica (§9.2, Phase 8.1).

### Data ownership and boundaries

- Which databases hold what? Default: keep the legacy database untouched,
  and give the facade its own database for session state only. Two
  databases, two least-privilege users, no shared tables. The legacy
  system's schema is not yours to grow, and a session table inside it is
  the beginning of a coupling you cannot later undo.

## 8. Legacy-system discovery: the facts everything else depends on

Every decision in §9 branches on something about the legacy system, and
**vendor documentation and actual behaviour routinely disagree.** Verify
each of the following by calling it, not by reading about it. This is
Phase 0's work (§13) and it is pure investigation — no code yet.

1. **Does it speak a standard protocol?** Look for a discovery document at
   a well-known path. Save a copy. If there is none, find out whether there
   is an OAuth/OIDC implementation at all, or only a login form.
2. **Which grant types does it support?** Note which are enabled by default
   and which require configuration. Advertised is not the same as
   implemented — a discovery document listing a grant type is not evidence
   that the grant works.
3. **Where are its signing keys published?** Usually a key-set (JWKS)
   endpoint. Confirm you can fetch them, and note the URL as a separate
   outbound dependency — it will need its own circuit breaker (§14).
4. **Does it have a logout endpoint, and what exactly does it require?**
   Parameter names, whether a post-logout redirect must match exactly,
   whether it needs a token hint. **Read its source if you have it**;
   logout contracts are frequently undocumented. In §5's migration the
   legacy system compared the post-logout redirect URI with a plain
   string equality in SQL — not a host allow-list, not a prefix match —
   so a trailing slash or a different scheme broke logout silently, with no
   error surfaced anywhere. That cost a day to find.
5. **Does it support server-to-server token revocation?** Most legacy
   systems do not. If it does not, write that down now: it becomes a
   permanent, structural limitation of everything you build. Your
   revocation will end *your* sessions; the legacy system's own token stays
   valid until it expires on its own, and no amount of work on your side
   changes that (§9.2, Phase 3.4).
6. **How does it register client applications** — a UI, a database table, a
   config file, an API? Who owns that registration going forward? If you
   write registrations directly into its database, check how its own code
   writes them (encryption, hashing, defaults) and match it exactly.
7. **What are its real credential rules?** Not the policy for new accounts
   — the rules obeyed by accounts that already exist, which may predate any
   policy whatsoever.
8. **How does it behave on a wrong password?** Specifically: does it return
   a clean rejection, or does it sometimes return a server error? §5's
   legacy system returned a 500 on some wrong-password attempts, which
   matters more than it sounds: a circuit breaker that counts server errors
   as failures will trip on ordinary failed logins unless you handle it. Note
   every defect you find, and decide deliberately whether to compensate for
   it, fix it upstream, or accept it in the ledger (§11).

**Done when** you have a short written document answering all eight, each
with evidence — a saved response, a source file reference, a reproduction
you can re-run.

The two most commonly skipped are 4 and 5, because logout and revocation
feel like later problems. They are not: they constrain your session design,
which is one of the first things you build.

## 9. Decision graphs

The four decisions below are the hardest to reverse later, so they are laid
out as explicit trees rather than prose. Start at the top question and
follow the branch that matches your situation; the leaf you land on is the
recommendation, with the reasoning beside it.

If you are short on time, §9.1 and §9.2 are the two that shape everything
else.

### 9.1 How should the facade relate to the legacy identity system?

```text
Does the legacy system already speak a standard protocol (OIDC/OAuth2/SAML)?
│
├─ No → Will you be allowed to modify the legacy system?
│        ├─ Yes → Add a standard protocol endpoint to it. Smaller blast
│        │        radius than building a parallel identity system, and it
│        │        leaves one authority for credentials.
│        └─ No  → Build a verification adapter in front of it: delegate
│                 credential checks to whatever interface it does expose,
│                 and never duplicate the credential store. You will own
│                 more quirk-handling than a protocol client would, so
│                 isolate it in one place.
│
└─ Yes → Do any clients need the legacy system's own token (bearer, direct
          API calls as that identity)?
          ├─ No  → Build a BFF: redirect-based login, session cookie only,
          │        token kept server-side. This is the default — prefer it.
          └─ Yes → Pass the legacy token through unchanged rather than
                    minting your own, AND still keep it off the browser if
                    the caller is itself a browser-facing app: attach the
                    token server-side on the one hop that actually needs
                    it, never hand it to the client.
```

Why minting your own token is the branch to avoid: the moment your facade
signs its own identity assertions, it owns token semantics, expiry, and key
rotation, and it has become a second authority on identity — the exact
thing §4 says the strategy exists to prevent. §5's migration did this
first and undid it within weeks.

### 9.2 Where does session state live?

```text
Must a session be revocable before natural expiry (compromise response,
admin action, logout-everywhere)?
│
├─ No, and single-instance is acceptable for the foreseeable scope
│   → A self-contained encrypted cookie is acceptable short-term. Write
│     this limit down explicitly, so nobody later assumes revocation
│     exists when it does not. The failure mode here is not technical: it
│     is someone promising a customer that sessions can be killed.
│
└─ Yes, OR you need more than one instance / rolling deploys
    → Server-side session record, opaque cookie key only.
      Needs three things, all of which are easy to forget:
        · a shared backing store (not a per-process file),
        · a shared encryption/signing key ring (not in-process memory),
        · persisted, not ephemeral, certificates.
      Revocation then becomes "delete the row" — no cooperation needed
      from whoever holds the cookie, and none from the legacy system
      either, for your own side of the session.
```

Note the asymmetry this leaves, and put it in the ledger (§11): deleting
your session record ends your session immediately, but if the legacy system
has no server-to-server revocation (§8, question 5), the token it issued
remains valid for its natural lifetime. You have revoked your half. Say so
plainly rather than describing the feature as "revocation" without
qualification.

### 9.3 Is it time to delete the stand-in?

```text
Does anything in the running system still call the stand-in path
(a password grant, a legacy direct-login form) in production traffic?
│
├─ Yes → Not yet. Keep it, and keep its removal criterion written down.
│         Then go and fix the caller: the real flow already exists.
│
└─ No  → Is the reason you are keeping it a *real, scheduled* need (a
          client that exists today) or a *hypothetical future* one?
          ├─ Hypothetical ("maybe mobile will need it", "maybe we'll need
          │   machine-to-machine later") → Delete it now. A real future
          │   need gets its own decision against what is actually required
          │   then. Keeping unused auth surface "just in case" is attack
          │   surface with no owner, no monitoring, and no maintainer.
          └─ Real, scheduled → Keep it, scoped to that one client only,
                                and revisit on the date it was scheduled
                                for — not "later."
```

### 9.4 BFF forever, or a dedicated identity service?

This is the fork most legacy-auth migrations hit eventually. §5's migration
deferred it deliberately behind a milestone gate rather than answering it
early, which is the right move: the question is much easier to answer once
you know what the new services actually need.

```text
Does the facade need to become the system of record for identity — own
accounts, own password reset, own multi-factor enrolment — rather than
only brokering sessions against the legacy system?
│
├─ No → Stay a BFF/bridge. Cheaper, smaller attack surface, no duplicated
│        user data, no credential migration. Revisit only when a concrete
│        requirement forces the question: the legacy system is being
│        decommissioned, or something is required that it cannot support
│        (SSO federation, a per-tenant identity provider, step-up auth).
│
└─ Yes → Dedicated identity service. Needs, at minimum:
          · a migration plan for existing credentials — either hash-format
            compatibility, or a forced reset for every user, which is a
            product decision as much as a technical one;
          · a decision on who owns the OAuth client registration going
            forward, since it currently lives in the legacy system;
          · a cutover plan for sessions already issued by the facade.
```

## 10. Tradeoffs

Each row is a genuine fork where both options are defensible in some
context. The last column is what to pick absent a specific reason to do
otherwise — and a deviation is exactly the kind of thing that belongs in
the ledger (§11).

| Choice | Pro | Con | Default recommendation |
|---|---|---|---|
| Token broker (mint your own) vs. pass through the legacy token | Broker: a stable contract for clients regardless of legacy quirks | Broker: you now own token semantics, expiry, and signing — a second source of truth for identity | Prefer pass-through; better still, no client-visible token at all (BFF) |
| BFF (cookie only) vs. handing clients a bearer token | BFF: the browser never holds a credential worth stealing via a script injection | BFF: every browser call must round-trip through the facade's proxy; no direct client-to-legacy-API calls | BFF, for browser clients, unconditionally |
| Stateless cookie vs. server-side session record | Stateless: no database, scales trivially | Stateless: no revocation without a separate blocklist — and the same problem resurfaces the moment revocation is required | Server-side, the moment revocation is a real requirement |
| Sticky sessions vs. shared state for multi-instance | Sticky: cheap, no new infrastructure | Sticky: a rolling deploy or a replica failure reproduces the exact failure it was meant to hide. Routing, not state, is what it fixes | Shared state; sticky sessions only as a named, temporary stop-gap |
| Keep a deprecated grant "just in case" vs. delete once unused | Keep: zero risk of breaking a caller you did not know about | Keep: permanent attack surface and test burden for a caller that may never materialise | Delete; a real future need gets its own decision later |
| Build your own resilience primitive vs. reuse a library pattern | Reuse: the pattern survives even when the call site it guarded is deleted | Reuse: dead decorators pointed at deleted interfaces are not a safe intermediate state if you leave the wiring behind | Delete the wiring with the call site; keep the pattern to reapply when a new call site needs it |
| Fix multi-instance support now vs. defer | Now: no future surprise when scaling | Now: speculative work against a requirement that does not exist yet | Defer, with the exact preconditions for revisiting written down |

## 11. The accepted-risk ledger

The single most reusable practice in this handbook, and the cheapest:
**maintain one document that lists what you deliberately did not fix, next
to what you did**, so every risk is either addressed or *consciously
owned* — never forgotten by omission.

In §5's migration this was the second half of the capstone document, and it
turned out to be the more valuable half. Its entries were things like: the
legacy system exposes no server-to-server revocation, so our revocation
never reaches its token; multi-instance operation is unblocked but never
actually tested; authorization is not built at all; admin revocation is
gated by a configured allow-list rather than a real role; an upstream
defect returning a server error on some wrong passwords has no regression
guard.

None of those are flattering. All of them were better written down than
discovered by someone else.

Maintain the ledger from day one and revisit it at every milestone. Each
entry answers three questions:

- **What did we decide not to build, and why was that the right call *at
  the time*?** The "at the time" matters — a decision that was correct
  under last quarter's constraints is not wrong, it is just possibly stale.
- **What would have to be true for this to need revisiting?** This is what
  turns a risk into something that can actually be re-evaluated rather than
  re-argued from scratch.
- **Who or what inherits this risk downstream?** A new service, a new
  client type, a new environment. Each inheritor should know what it is
  taking on.

A good ledger entry names the specific gap — "the legacy system exposes no
server-to-server revocation endpoint, so our revocation never reaches it" —
not a vague "security could be improved." The second kind is
indistinguishable from having no ledger.

---

# Part III — The build order

## 12. How to use the build order

Part II told you what the decisions are. This part tells you what to build
next. Ten phases, in order; each step has a verifiable finish line.

Each step follows the same shape:

- **Goal** — one sentence on what this step produces.
- **Why now** — why it comes here and not earlier or later.
- **Do this** — the actual work, in order.
- **Done when** — observable, verifiable exit criteria. If you cannot
  demonstrate these, you are not done, regardless of how much code exists.
- **Common mistakes** — what goes wrong at this step specifically.
- **Don't do yet** — work that belongs to a later step. This matters as
  much as the "do" list: most trouble in this kind of project comes from
  doing Phase 7's work during Phase 2.

**Follow the phases in order.** Within a phase the steps are usually
sequential too; where two can be worked in parallel it says so.

**The single most important discipline: finish a step's "Done when" before
starting the next one.** Each phase assumes the previous one actually
works, and debugging a half-finished login flow underneath a half-finished
session layer is dramatically harder than debugging either alone.

```text
Phase 0   Learn the legacy system            ← pure investigation, no code
   │
Phase 1   Verify a credential, end to end    ← the throwaway stand-in
   │
Phase 2   Real browser login flow            ← the actual login
   │
Phase 3   Make the session real              ← revocable, durable, refreshing
   │
Phase 4   Let the frontend reach legacy data ← server-side proxy
   │
Phase 5   One public origin + TLS            ← gateway in front
   │
Phase 6   Delete the stand-in from Phase 1   ← do not skip this
   │
Phase 7   Harden                             ← limits, breakers, audit
   │
Phase 8   Prove it scales                    ← more than one instance
   │
Phase 9   Production gate                    ← separate, higher bar
```

Rough expectation: phases 0–2 are where you learn most of what you did not
know. Phases 3 and 7 are the bulk of the work. Phase 6 takes an afternoon
and is the one teams skip.

## Phase 0 — Learn the legacy system

No code in this phase. Everything you build later depends on facts you do
not have yet, and guessing them is the single most common source of rework.

### 0.1 Get a local, disposable instance of the legacy system running

**Goal:** a legacy system you can break, reset, and inspect freely.

**Why now:** you cannot discover what it supports (0.2) by reading its
documentation — vendor docs and actual behaviour routinely disagree, and
the actual behaviour is what you have to build against.

**Do this:**

1. Stand up the legacy system locally, in a container or VM, with synthetic
   data only.
2. Make it reproducible from scratch — a script or compose file, not a
   hand-assembled machine you will be afraid to delete.
3. Confirm you can reset it to a known state, and that a fresh start
   produces a working login through its own UI.

**Done when:** you can destroy the whole thing and have it back, working,
with one command — and you have done it at least once.

**Common mistakes:**

- Sharing one instance across the team. You will each need to break it.
- Using a copy of production data. Synthetic or properly sanitised data
  only; nothing in this phase needs real records.

**Don't do yet:** any integration. Just get it running.

> If the legacy system's first boot takes minutes rather than seconds —
> which is common, and is about its *installer* rather than its container —
> §21 is the technique for making that a one-time cost instead of a
> per-run one. It is worth reading before you have built a pipeline around
> the slow path.

### 0.2 Find out what it actually supports

**Goal:** a written list of the legacy system's authentication
capabilities, verified by observation rather than documentation.

**Why now:** this list determines which branch of every decision graph in
§9 applies to you.

**Do this:** work through the eight questions in §8, verifying each by
actually calling the thing rather than reading about it.

**Done when:** you have a short written document answering all eight, each
with evidence.

**Common mistakes:**

- Trusting the discovery document's advertised capabilities without calling
  them. Advertised is not implemented.
- Skipping logout and revocation because they feel like later problems.
  They constrain your session design, which is Phase 3.

**Don't do yet:** choosing libraries or writing an architecture document.

### 0.3 Register a client application, reproducibly

**Goal:** the legacy system has a registered client for your facade, and
the registration is created by a script you can re-run.

**Why now:** every later phase authenticates as this client. A
hand-registered client nobody can recreate becomes a blocker the first time
someone else sets up the project.

**Do this:**

1. Register a client with the minimum scopes needed to identify a user.
   Resist adding more "for later" — widening scope is a deliberate,
   separate change (4.1).
2. Make the registration a script, and make it **idempotent**: it should
   create the client if missing and update it if present, so running it
   twice is safe and running it again after changing a value actually fixes
   the registration.
3. Generate the client secret; never type a memorable one, and never commit
   it.
4. Note exactly which fields your script sets and which it does not. If
   some field cannot be updated after creation, write that down — it is a
   trap for whoever changes that field later.

**Done when:** running the script twice in a row succeeds both times and
leaves the same correct result, and a teammate can set up from scratch
using it.

**Common mistakes:**

- Writing the registration directly into a database without matching how
  the legacy system itself writes it — encryption, hashing, defaults.
  Check how its own code creates a client, and match it.
- Over-scoping the client because it is easier than coming back later.

### 0.4 Decide and write down your stand-in's removal criterion

**Goal:** one sentence in your repository stating what has to be true
before you delete the Phase 1 stand-in.

**Why now:** this is the only moment this will ever be easy to write.
Afterwards the stand-in works, and nobody wants to touch it.

**Do this:** write, in a decision record or the README, something of this
shape: *"The [stand-in] exists only to unblock [X]. It will be deleted when
[the real browser flow works end to end through the gateway]. It is not to
be used by any new client."*

**Done when:** it is committed, and the team has seen it.

---

## Phase 1 — Verify a credential, end to end

**The point of this phase is learning, not architecture.** You are proving
you can make the legacy system answer "is this person who they claim to
be," and finding out what it does when things go wrong. Everything built
here is scaffolding you will delete in Phase 6.

### 1.1 Build the smallest possible credential check

**Goal:** one endpoint on your facade that takes a username and password,
asks the legacy system, and returns success or failure.

**Why now:** it is the shortest path to a working round trip, which is what
teaches you the legacy system's real behaviour.

**Do this:**

1. Add one endpoint that accepts credentials.
2. Call whatever the legacy system offers for direct credential
   verification — commonly a password grant on its token endpoint.
3. Return a minimal success/failure result. Do not design a response
   format; it is temporary.
4. Add bounds on input length, matched to what the legacy system can
   actually store — as a guard against absurd input, not as a password
   policy.

**Done when:** a correct credential returns success and an incorrect one
returns a clean failure, both verified by hand against the real local
legacy system.

**Common mistakes:**

- **Enforcing a password policy at login.** Existing accounts may violate
  any policy you invent, and you will lock out real users. Policy belongs
  where accounts are created, which is not here. Your facade must never
  reject a credential the legacy system would have accepted.
- Designing a token or response format. It is getting deleted.

**Don't do yet:** sessions, cookies, tokens for clients, redirects.

### 1.2 Prove credentials never reach a log

**Goal:** confidence, backed by a test, that no credential appears in any
log output.

**Why now:** this is the phase where credentials actually pass through your
process. It is also the easiest moment to build the test that keeps being
true later.

**Do this:**

1. Audit your own logging calls for anything that includes the request body
   or credential fields.
2. Write a test that drives a login with a recognisable marker value as the
   password, **with logging turned up to its most verbose level**, and
   asserts the marker appears nowhere in captured output. Use the most
   permissive level the system could ever run at, not the default — the
   risk lives at verbose levels, and a misconfigured deployment can reach
   them.
3. Make that test **also assert that some log output was captured at all**,
   before asserting the marker's absence. Otherwise, if your capture
   mechanism silently breaks later, the test passes forever for the wrong
   reason — there was simply nothing to search.
4. If you must hold the plaintext at all, retrieve it once into a local
   variable, use it, and clear it. Do not pass it around.

**Done when:** the test exists, passes, and fails if you deliberately add a
logging statement that includes the password.

**Common mistakes:**

- Testing at the default log level only.
- Assuming your own code is the only thing that logs. Frameworks and HTTP
  client libraries log too, and they log request details.

### 1.3 Add one integration test against the real legacy system

**Goal:** an automated test that fails if the legacy system stops accepting
your client or your credential check.

**Why now:** you are about to build a great deal on top of the assumption
that this works. Make it a test, not a memory of a manual check.

**Do this:**

1. Write a test that runs against the real, running local legacy system —
   not a fake.
2. Have it verify that a correct credential succeeds, and that an incorrect
   one fails in the specific way you observed in §8, question 8.
3. Mark or categorise it so it can be run separately from fast unit tests;
   it needs the stack up.

**Done when:** the test passes with the stack running, and is clearly
separated from tests that do not need the stack.

**Don't do yet:** a readiness fixture or elaborate test infrastructure. One
test, run manually, is enough at this point (§16.4 is for when it is not).

---

## Phase 2 — The real browser login flow

This is the login you are actually shipping. The user will type their
password into the legacy system's own login page, and your facade will
never see it.

### 2.1 Choose a protocol library — do not hand-roll

**Goal:** a library in your project that implements the authorization-code
flow, with PKCE, as a *client*.

**Why now:** before you write any flow code, because the library's shape
will influence how you structure the endpoints.

**Do this:**

1. Pick a well-maintained OAuth2/OIDC **client** library for your stack.
   You are the client here; the legacy system is the server.
2. Verify it supports: PKCE, validating the identity token's signature
   against a published key set, nonce checking, and refresh.
3. Confirm how it stores the short-lived state it needs between the
   redirect out and the redirect back. **Note where that lives.** If it is
   an in-process file or in memory, you have a single-instance constraint
   to resolve in Phase 8 — and in §5's migration this was a per-process
   temporary database file, which worked flawlessly right up until the
   first restart mid-login.

**Done when:** the library is added, and you have written down where it
keeps its interim state.

**Common mistakes:**

- Hand-rolling PKCE, state, and nonce handling. These are easy to get
  subtly, silently wrong, and the failure mode is a security hole rather
  than a broken feature.
- Picking a *server* (identity provider) library by mistake. You are not
  building an identity provider.

### 2.2 Add the login-start endpoint

**Goal:** an endpoint that redirects the browser to the legacy system's
login page with a correctly formed authorization request.

**Do this:**

1. Add a `/login`-shaped route that triggers the library's redirect to the
   legacy system's authorize endpoint.
2. Confirm the outgoing redirect contains the expected parameters,
   including the PKCE challenge, a state value, and a nonce.
3. Accept an optional "where to go after login" parameter — and **validate
   it against an allow-list of hosts you control**, falling back to a safe
   default when it does not match. An unvalidated post-login redirect is an
   open-redirect vulnerability.
4. Write a test that drives the **rejection** path of that validation: a
   foreign host, a non-HTTP scheme (`javascript:`, `data:`), a bare
   relative path treated as absolute, a malformed value. Not just the happy
   path.

**Done when:** hitting the endpoint in a browser lands you on the legacy
system's real login page, and the redirect validation's rejection path is
covered by a test.

**Common mistakes:**

- Forgetting the allow-list on the return URL, or writing it and then only
  testing that valid values pass. This specific guard went untested for
  months in §5's migration — short, obviously-correct-looking code that
  nothing else in the suite happened to exercise. §17.5 is about how to
  find that class of gap deliberately.

### 2.3 Add the callback endpoint and validate properly

**Goal:** an endpoint the legacy system redirects back to, which exchanges
the code for tokens and verifies the identity token.

**Do this:**

1. Add the callback route, registered to exactly the URI the client
   registration in 0.3 declares. It must match byte for byte — check
   whether the legacy system compares exactly or by prefix (§8, question 4).
2. Let the library redeem the code server-to-server.
3. Ensure it validates, at minimum: the token's signature against the
   legacy system's published keys, the issuer, the audience, and the nonce
   you sent in 2.2.
4. On any failure, redirect to a **generic** error. Never surface the
   legacy system's own error text to the browser — it tells an attacker
   probing a login which part of the attempt failed.
5. Write one test per validation check, each with **everything else valid
   and only that one thing wrong**. A token that is invalid in every
   dimension at once proves nothing about which check caught it. The test
   that matters is a genuinely valid identity token — real signature, right
   issuer, right audience — with only the nonce wrong.

**Done when:** a real browser login completes, and you have a test for each
of signature, issuer, audience, and nonce failing independently.

**Common mistakes:**

- Assuming the library validates something it does not. Verify each check
  with a test rather than trusting defaults.
- Returning useful error detail to the browser.

### 2.4 Establish the session

**Goal:** a successful callback leaves the browser holding a session
cookie, and nothing else.

**Do this:**

1. Sign the user into a cookie-based session on callback success.
2. Set the cookie flags correctly: HTTP-only, same-site restricted, and
   secure. (Secure will not actually be true until Phase 5 adds TLS — write
   the code expecting it now.)
3. Add one protected endpoint that returns the current user's identity, so
   you can confirm the session works.
4. Keep the tokens the exchange produced — you will need them in Phase 4.
   Store them **inside the session**, never anywhere the browser can reach.

**Done when:** after a browser login the protected endpoint returns your
identity, and the browser's cookie store contains only an authentication
cookie — no access or refresh token anywhere in the browser.

**Common mistakes:**

- Returning tokens in the callback response "so the frontend can use them."
  This is the single decision that most undermines the whole design. The
  frontend does not need them; Phase 4 solves that properly.
- Having two authentication mechanisms live at once — the Phase 1
  stand-in's and this one — without being explicit about which one
  protected endpoints actually require. In §5's migration a policy change
  once silently stopped authenticating the cookie scheme altogether, which
  is exactly the kind of regression that hides when two schemes coexist.
  Check explicitly, per route.

### 2.5 Verify the browser never receives a token

**Goal:** a test that fails if a token ever appears in a browser-visible
response.

**Do this:** write a test that completes a login and inspects every
response body, header, and cookie for the token values the legacy system
issued. Assert they appear nowhere.

**Done when:** the test passes, and fails if you deliberately add the token
to a response.

---

## Phase 3 — Make the session real

Right now you have a session that works until something restarts, cannot be
revoked, and expires awkwardly. This phase fixes all three. It is the
largest phase; do the steps in order, since each builds on the last.

### 3.1 Decide: stateless cookie or server-side session record

**Goal:** a written decision, with its consequence stated.

**Why now:** everything else in this phase depends on the answer, and
changing it later means changing all of it.

**Do this:** answer one question — *must a session be revocable before it
naturally expires?* If yes, or if you will ever run more than one instance,
you need a server-side session record (§9.2). If genuinely no, a
self-contained encrypted cookie is acceptable, **and you must write down
that revocation does not exist**, so nobody later assumes it does.

**Done when:** the decision and its consequence are committed somewhere
durable.

### 3.2 Move the session's contents server-side

**Goal:** the cookie carries only an opaque key; the real session contents
live in a store you control.

**Do this:**

1. Give the facade its own database or store — separate from the legacy
   system's, with its own least-privilege credentials. It holds only
   session state. It is not a second copy of user data.
2. Move the session contents — identity claims, and the legacy tokens from
   2.4 — into one record per session.
3. Replace the cookie's payload with a reference to that record.
4. Point your protocol library's interim state storage (2.1) at the same
   store, replacing any per-process file.

**Done when:** you can look at a session cookie and see that it is
meaningless on its own, and deleting the corresponding record makes that
cookie stop working immediately.

**Common mistakes:**

- Putting session state in the legacy system's own database. Keep the
  boundary clean: the legacy system's schema is not yours to grow.
- Leaving the protocol library's interim state in a local file. That is one
  of the two things that breaks under multiple instances (Phase 8).

### 3.3 Make key material and certificates survive a restart

**Goal:** restarting the facade does not log everyone out.

**Why now:** before you build revocation and refresh on top, since both
assume sessions survive normal operations.

**Do this:**

1. Find every cryptographic key or certificate your facade generates at
   startup. Typically that is the key ring protecting session cookies, and
   any certificates your protocol library uses to protect its own interim
   state. In §5's migration both were ephemeral: the certificates were
   regenerated on every boot, so every restart invalidated every session,
   and nobody noticed for weeks because restarts in development always
   coincided with re-logging-in anyway.
2. Replace each with a persisted one: a file in durable storage, or rows in
   the store from 3.2, with a documented generation step for each.
3. Verify by experiment, not inspection.

**Done when:** you log in, restart the facade, reload the page, and are
**still logged in**. Then: start a login, restart the facade mid-flow —
after the redirect out, before the redirect back — complete it, and confirm
it still succeeds.

**Common mistakes:**

- Testing only the "already logged in" case, and missing that the
  mid-login interim state has its own, separately generated protection.
- Declaring this done by reading configuration. Restart the process and
  observe.

### 3.4 Add revocation

**Goal:** a session can be ended server-side without the cooperation of
whoever holds the cookie.

**Do this:**

1. Add a self-service endpoint that ends **all** sessions for the calling
   user's identity — not just the caller's own session. The threat model is
   "someone else has a copy of my session," and that copy is a different
   record.
2. Test it with two independent browser sessions for the same user, each
   with its own cookie jar: revoking from one must end both. Crucially,
   assert against the session the revoking call never touched — a test that
   only checks the revoking caller's own next request cannot distinguish
   real server-side revocation from a client-side no-op.
3. Test that it does **not** affect a different user's sessions. These two
   tests are a matched pair: the first guards under-reach (an attacker's
   second session surviving a revoke-all), the second guards over-reach
   (one user's action silently affecting another's).
4. Then add an admin-initiated version that ends another user's sessions.
   Gate it on something — even a configured allow-list is acceptable as a
   starting point, provided it **defaults to empty** (nobody can revoke
   anyone, failing closed) and you write down that it is not a real
   authorization model.
5. Test the unauthorized path: a non-admin attempt must be refused **and**
   leave the target's sessions alive. Assert both — do not stop at the
   refusal status, because a rejected response wrapped around a side effect
   that happened anyway looks identical from outside.
6. Test the self-targeting case separately: an admin revoking their own
   identity should be signed out too. "Can affect anyone" and "can affect
   anyone, including themselves" are easy to get different answers to by
   accident.
7. Remember the limitation from §8, question 5: if the legacy system has no
   server-to-server revocation, its own token stays valid until it expires.
   Your revocation covers your sessions, not the legacy system's. Write
   this in the ledger (§11) and do not describe the feature as "revocation"
   without that qualification.

**Done when:** all of these pass — revoke reaches every session of that
user; reaches no other user's; requires authentication on each endpoint
separately; unauthorized attempts change nothing; self-targeting works.

### 3.5 Add silent token refresh

**Goal:** an expired legacy access token renews itself without the user
noticing.

**Do this:**

1. Before using a stored access token, check whether it is expired or close
   to it; if so, redeem the refresh token and update the session record.
2. On refresh failure, **fall back to the stale token** and let the legacy
   system's own rejection decide the outcome. Do not invent your own
   failure — the legacy system is the authority on whether its token is
   still good, and guessing produces logouts that did not need to happen.
3. Test with a deliberately already-expired token so you do not have to
   wait out a real expiry.

**Done when:** a request made with an expired stored token succeeds, and
the session record afterwards holds a new token.

### 3.6 Add logout, on both sides

**Goal:** logging out ends the session in your facade *and* in the legacy
system.

**Do this:**

1. End the local session — delete the record from 3.2.
2. If the legacy system supports a logout endpoint (§8, question 4),
   redirect there afterwards with exactly the parameters it requires.
3. Confirm the post-logout redirect target is registered exactly as the
   legacy system expects to compare it. If it compares by plain string
   equality, a trailing slash is a silent failure.

**Done when:** after logout the protected endpoint rejects you, and
starting a new login prompts for credentials again rather than silently
signing you straight back in.

**Common mistakes:**

- Only ending your own session. The user is still logged into the legacy
  system, so the next login appears to skip authentication entirely —
  alarming to watch, and a real risk on a shared machine.

---

## Phase 4 — Let the frontend reach legacy data

Your frontend now has a session but no way to call the legacy system's API,
because — correctly — it has no token.

### 4.1 Add a server-side proxy

**Goal:** the frontend calls your facade; your facade forwards to the
legacy API with the session's token attached server-side.

**Do this:**

1. Add a route prefix on the facade that forwards matching requests to the
   legacy system's API.
2. On each forwarded request, look up the session's access token (via 3.5,
   so it is fresh) and attach it to the outbound request.
3. Relay the legacy system's response back unchanged, including its error
   statuses. Do not reshape or swallow them.
4. Confirm the scopes your client requested (0.3) actually cover the API
   calls you are forwarding. If they do not, widen them **deliberately,
   now, as its own change** — never preemptively "to be safe for later,"
   and never quietly mid-feature.

**Done when:** the frontend successfully calls a legacy API endpoint having
never held a token, and a legacy error status arrives at the frontend
intact.

**Common mistakes:**

- Forwarding the browser's cookie to the legacy system instead of the
  token. Different systems, different credentials.
- Discovering mid-phase that your client's scopes were too narrow and
  quietly widening them everywhere.

### 4.2 Decide what the proxy forwards to

**Goal:** a decision, recorded, about how this proxy grows.

**Do this:** decide now whether the proxy forwards to each backend service
by name, or to a single gateway that routes onward. **Prefer the gateway:**
adding a service later then requires no change to the facade at all,
whereas a facade that knows every service by name has to learn each new
one.

**Done when:** the decision is written down. The gateway does not need to
exist yet (Phase 5 builds it).

### 4.3 Prove request bodies never reach a log

**Goal:** the same guarantee as 1.2, now for the data flowing through the
proxy.

**Why now:** this is the path real user data will travel, and it is a
different code path from Phase 1's, so it needs its own test.

**Do this:** repeat 1.2's test shape — plant a marker in a proxied request
body, log at maximum verbosity, assert the marker appears in no log entry,
and assert some log output existed to search.

**Done when:** the test passes, and fails when you deliberately log the
body.

---

## Phase 5 — One public origin and TLS

### 5.1 Put a gateway in front

**Goal:** the browser talks to exactly one address for both the frontend
and the facade's own endpoints.

**Why now:** sessions are origin-scoped. Serving your frontend and your
login from different origins causes cookie problems that are confusing to
diagnose and tempting to "fix" by loosening cookie restrictions — which
undoes Phase 2's work.

**Do this:**

1. Add a reverse proxy/gateway in front of both, routing by path.
2. Give it **no identity logic of its own** — it routes, it does not
   decide.
3. Update the client registration (0.3) so the registered redirect URI is
   the **gateway's** address, not the facade's own.

**Done when:** a complete login works through the gateway's address only,
and the facade's own address is no longer needed by a browser.

**Common mistakes:**

- Forgetting to update the registered redirect URI, producing an
  invalid-client error that looks like a credential problem and sends you
  hunting through secrets. §16.2's "ask the legacy system directly" test
  exists precisely to catch this before a human does.

### 5.2 Terminate TLS and trust forwarded headers carefully

**Goal:** the whole flow is HTTPS, and the facade knows the real client
address and protocol.

**Do this:**

1. Terminate TLS at the gateway. Redirect plain HTTP; send HSTS.
2. Have the gateway pass the original protocol and client address as
   forwarded headers.
3. In the facade, **trust those headers only when the request comes from
   the gateway's network**, identified by a fixed, configured address
   range. Anything else must be ignored.
4. Make that configuration required: an invalid or missing trusted-range
   setting must **refuse to start the process**, never fall back to
   trusting everyone.
5. Test both directions: the header honoured from inside the trusted range,
   and ignored from outside it — same header, same claimed value, only the
   caller's network membership changed. A suite that tests only the trusted
   path cannot distinguish "the trust check works" from "the header is
   always honoured."

**Done when:** both tests pass, the process refuses to start with a broken
trusted-range setting, and the session cookie's secure flag is genuinely
true in a real browser.

**Common mistakes:**

- Trusting forwarded headers unconditionally. They are trivially forged,
  and your rate limiting (7.1) depends on the client address being real.
- Assuming your framework's built-in forwarded-header handling restricts by
  network correctly. **Verify it with a request from outside the range.**
  In §5's migration the framework's built-in option did not actually
  restrict anything on the installed runtime — confirmed in an isolated
  reproduction outside the codebase — and was replaced by a small,
  directly auditable middleware doing the one check by hand. The general
  rule: trust nothing security-critical from a library without a
  reproduction.

---

## Phase 6 — Delete the stand-in

The shortest phase, and the one most often skipped. The Phase 1 stand-in
now has no reason to exist, and its removal criterion (0.4) is met.

### 6.1 Prove nothing calls it

**Do this:**

1. Search the codebase for callers. Check your frontends specifically.
2. Check access logs for real traffic to it.
3. If something does still call it, fix that caller first. The real login
   flow has existed since Phase 2.

**Done when:** you can state, with evidence, that the only remaining
callers are its own tests.

### 6.2 Delete it, and everything that existed only for it

**Do this:**

1. Delete the endpoint and its request/response types.
2. Delete the authentication mechanism it introduced, if nothing else uses
   it. Often the facade is left with exactly one authentication scheme,
   which simplifies everything downstream — and removes the ambiguity 2.4
   warns about.
3. Delete modules and layers that are now empty. An empty layer is
   scaffolding protecting nothing. §5's migration removed three of five
   architectural layers at this step, all of which existed only to serve
   the stand-in.
4. Delete its tests outright rather than disabling them.
5. Regenerate any committed generated artifacts — API schemas, typed
   clients — that referenced it.

**Done when:** the build is green, a full browser login still works end to
end, and nothing in the repository mentions the stand-in except your
decision record.

**Common mistakes:**

- Keeping it "for a future mobile client." Mobile uses the Phase 2 flow,
  launched in a system browser. This is not a reason.
- Keeping it "for future server-to-server calls." That is a genuinely
  different problem with different requirements; solve it when a real
  caller exists, against what the legacy system supports then.
- Leaving empty modules behind as placeholders.

### 6.3 Record what coverage you lost

**Goal:** honesty about the gap removal created.

**Do this:** if deleting those tests removed the only coverage of something
still in use — a common surprise — say so explicitly in the commit or
decision record, and decide whether to replace it now or accept the gap
deliberately. In §5's migration, deleting the stand-in's tests removed the
only live, non-unit coverage of the cookie-protected identity endpoint
against the real stack, and the decision record said so plainly rather than
letting the suite quietly shrink.

**Done when:** the gap is either filled or written into the ledger (§11).

---

## Phase 7 — Harden

These steps are largely independent — parallelise freely. Each ends in a
test, because every one of them is a security control that silently stops
working if nothing watches it.

### 7.1 Rate-limit the unauthenticated entry points

**Do this:**

1. Apply a limit, per client address, to the login-start and callback
   endpoints.
2. Give the whole flow **one shared budget**, not one per endpoint.
   Otherwise alternating between them doubles the effective limit — two
   requests are two endpoints but one logical login attempt.
3. Reject immediately when over budget rather than queueing, and include a
   retry-after indication. Queueing a flood of login attempts only delays
   the same outcome; a different class of endpoint might reasonably choose
   otherwise, but make the choice rather than inheriting a library default.
4. Make the limit configuration required and validated: degenerate values
   (zero permits, sub-second windows) must refuse to start the process.
5. Test three shapes: exactly at the limit passes and one more is refused;
   alternating endpoints cannot exceed the shared budget; and an unrelated
   authenticated high-traffic endpoint is **not** caught by this policy.
   The third guards the opposite bug from the first — a limiter applied too
   broadly, silently breaking a legitimate high-frequency caller.

**Done when:** all three test shapes pass.

### 7.2 Add circuit breakers around every call to the legacy system

**Do this:**

1. Identify every outbound call: the token exchange, the key-set fetch, and
   the proxy's forwarded calls. Each gets its own breaker.
2. Wrap each in a circuit breaker whose state is **shared across
   requests** — one long-lived instance per dependency. A breaker
   constructed fresh per call can never trip, because its whole value is
   state that spans requests. This is the single easiest way to implement a
   circuit breaker that does nothing at all.
3. Count only real failures: server errors and timeouts. An ordinary
   rejection — a bad credential, a not-found from a proxied call — must
   never count toward opening the circuit. This matters more if your legacy
   system has the defect named in §8, question 8 — returning a server
   error on some wrong passwords — because then ordinary failed logins look
   like outages.
4. When open, fail fast with a generic error rather than hanging.
5. Test both halves: repeated server errors open it and stop calls reaching
   the legacy system; repeated ordinary rejections never open it, however
   many times they happen. It is easy to write "the breaker opens" and
   never write "and does not open for the things that should not count" —
   the second test is the one that catches a wrong failure predicate.

**Done when:** both tests pass, and you have verified the real behaviour by
stopping the legacy system mid-session and observing a fast, clean failure
instead of a hang.

### 7.3 Add an audit trail

**Do this:**

1. Log, to one dedicated and **stably named** category: login successes,
   login failures, proxied data access, and any admin action (3.4). See
   §18.1 for why the category name needs to be refactor-proof.
2. Record who, what, and when — never a credential, a token, or a
   request/response body.
3. For failures, log a **short fixed internal label**, not the legacy
   system's own error text. A log reader may be a different trust tier than
   whoever should see the legacy system's internal errors.
4. Decide deliberately what you are *not* auditing, and write down why. A
   high-volume endpoint that reveals nothing sensitive — an identity check
   firing on every page load — is a reasonable exclusion. An unexamined one
   is not.

**Done when:** a login, a proxied call, and an admin revocation each produce
exactly one audit entry with the expected fields, and 1.2 and 4.3's tests
still pass.

### 7.4 Make startup ordering explicit

**Do this:**

1. Have the facade wait for the legacy system's readiness before serving.
   Make this **fail-open**: if it times out, log a warning and start
   anyway. A request that then fails fails no worse than it would have
   without the check, and a host that never starts is worse than one that
   started slow.
2. Declare, in your orchestration, that the facade depends on the legacy
   system being *healthy* (not merely started) and on the provisioning
   steps (0.3, 3.2) having *completed successfully*. See §20.4 — these are
   genuinely different dependency relationships and picking the wrong one
   is a one-keyword mistake.
3. Make the provisioning scripts themselves **fail-closed**: exit non-zero
   on timeout, so nothing proceeds on half-finished setup. Note the
   deliberate asymmetry with step 1 — the app fails open, provisioning
   fails closed, for different reasons.

**Done when:** bringing up the entire stack from nothing, in one command,
reliably produces a working login — with no manual step and no "run it
again, it works the second time."

### 7.5 Sweep for fail-closed configuration

**Goal:** every security-relevant setting refuses to start rather than
degrading quietly.

**Do this:** list every configuration value that controls a security
decision — trusted network range, rate limits, admin allow-list,
certificate paths, redirect allow-list. For each, confirm that invalid
values refuse to start, and that a missing value either refuses to start or
defaults to the **restrictive** option (an empty allow-list means nobody,
never everybody). Add the startup test for each one.

**Done when:** each has a test asserting that invalid configuration
prevents startup. From here on, add that test in the same change that
introduces a new security-relevant setting, not as follow-up (§17.6).

---

## Phase 8 — Prove it scales

Everything until now has quietly assumed one process. Find out what breaks
before an incident does.

### 8.1 Audit for per-process state

**Do this:** list everything generated per process or stored locally —
cryptographic key rings, certificates, the protocol library's interim
state, caches, temporary files. For each, answer: is it now shared or
persisted (3.2, 3.3), or still per-process?

**Done when:** the list exists and every item is either shared/persisted or
explicitly accepted as a remaining blocker, in the ledger.

### 8.2 Actually run two instances

**Why this is its own step:** removing the blockers is not the same claim as
proving the result. Until you do this, nobody has ever run two of these.
§5's migration closed every structural blocker and still listed
"multi-instance untested" as an accepted risk, which was the honest
position.

**Do this:**

1. Run two facade instances behind a load balancer with **no** session
   affinity.
2. Complete a login, confirming it works when the redirect back lands on a
   different instance than the one that started it.
3. Make authenticated requests repeatedly, confirming they succeed
   regardless of which instance serves them.
4. Revoke a session from one instance; confirm the other honours it
   immediately.

**Done when:** all four work. If any fail, the cause is almost certainly
something on 8.1's list — most often the protocol library's interim state
or the cookie key ring.

**Common mistakes:**

- Enabling session affinity to make it pass. That hides the problem rather
  than fixing it, and a restart or a rolling deploy reproduces the exact
  failure affinity was meant to prevent — routing, not state, is what it
  fixes. Affinity is an acceptable *documented interim measure*, never a
  fix.

### 8.3 Test a rolling deploy mid-flow

**Do this:** start a login, replace the instance handling it mid-flow, then
complete the login. Separately: hold an active session through a full
rolling replacement of every instance.

**Done when:** both survive. This is the test that distinguishes "works
with two instances" from "works in production."

---

## Phase 9 — Production gate

Do not treat "Phase 8 passed" as "ready for real users." Production adds
real secrets, real network exposure, real data, and real consequences.

The gate itself is §24. In summary, the categories **not** covered by
anything in Phases 0–8:

- Secrets from a real secrets manager, with rotation — not a local config
  file with shared credentials.
- Real certificates with a no-downtime rotation procedure.
- A genuine authorization model (what an identity may *do*), which this
  entire handbook deliberately does not build.
- Metrics, tracing, and alerting — structured logs alone are not
  observability.
- Tested backup and restore of the session store.
- An incident runbook for a suspected compromised session.
- Resource limits derived from a real load test.
- Dependency and container image scanning in the pipeline.
- A security review of the *production* configuration specifically.
- A rollback plan covering active sessions and in-flight logins.

## 13. Working practices, and signs you are off track

Independent of phase:

- **Record each real decision as you make it**, briefly: what you chose,
  what you rejected, and why. The "why" is what makes it possible to
  revisit the decision intelligently later — and most of these decisions
  get revisited.
- **Keep the risk ledger from day one** (§11). Every time you decide *not*
  to fix something, write it down with what would have to change for it to
  matter. A risk that is written down is owned; one that is not is
  forgotten.
- **Prefer deleting to disabling.** Commented-out code, disabled tests, and
  unused endpoints all read as "someone might need this," which means
  nobody maintains them.
- **Verify by observation, not inspection.** Restart the process. Stop the
  dependency. Send the forged header. Reading configuration proves nothing
  about behaviour, and this is a component where being wrong is expensive.
- **When something surprising works, find out why before moving on.** In
  this kind of integration, "it works now" and "I understand why it works"
  diverge often, and the gap always closes eventually — usually at an
  inconvenient moment.

Symptoms, and where to go:

| Symptom | What it means |
|---|---|
| Your frontend has a token | 2.4/2.5 are not done. This is the one that matters most. |
| The stand-in is still there and Phase 2 works | Go to Phase 6 today. |
| Restarting logs everyone out | 3.3 is not done — something security-critical is still generated per process. |
| "Revoke" only ends the caller's own session | 3.4 is not done; you have a logout, not a revocation. |
| A second instance breaks login intermittently | An unresolved item on 8.1's list, most often the protocol library's interim state or the cookie key ring. |
| Setup needs a manual step, or works on the second attempt | 7.4 is not done, and every new team member will hit it. |
| You are building an authorization model | That is a different project. Make sure it is a deliberate decision rather than scope drift. |

---

# Part IV — Reference: making it real

## 14. Hardening requirements

Treat this as a checklist of *categories*. A design that skips one should
say so explicitly as an accepted risk (§11), not silently. Each row states
the requirement and why it exists, so the table can be used as an audit of
a facade someone else built.

| Category | Requirement | Why |
|---|---|---|
| Transport | TLS terminated at a trusted boundary; plain HTTP redirected; HSTS sent; forwarded-header trust scoped to a known network, never blindly accepted | Forwarded headers are trivially forged, and your rate limiting and audit log both depend on the client address being real |
| Credential handling | Never log credentials or tokens, at any log level, in any logger category; if you must touch plaintext, retrieve it once and clear it | A verbose log level is reachable by misconfiguration, and logs are often retained longer and read by more people than the credential store |
| Rate limiting | Every unauthenticated entry point (login, callback) shares **one** budget per client address | Two endpoints are one logical login attempt; separate budgets silently double the limit |
| Resilience | A circuit breaker around every outbound call to the legacy system — token exchange, key-set fetch, each proxied call | One sick dependency otherwise hangs every caller in your system rather than failing fast |
| Session integrity | The cookie carries an opaque reference only; the real session ticket and any legacy token live server-side | A token in a browser can be stolen by any script on the page, and usually cannot be revoked |
| Revocation | A session can be killed server-side, both self-service and admin-initiated, with no cooperation from whoever holds the cookie | The threat model is "a copy of this session exists somewhere I do not control" |
| Logout | Logout ends the session on *both* sides when the legacy system supports it — read its actual contract from source or discovery rather than assuming | Ending only your own session leaves the user signed into the legacy system, so the next login silently skips authentication |
| Key/cert durability | Nothing security-critical is ephemeral per process: encryption keys, signing certificates, and any correlation/state store survive a restart | Otherwise a restart logs everyone out, and a restart mid-login fails the login outright |
| Audit | Login outcomes and any proxied data access logged to one filterable category, with zero credentials, tokens, or bodies in it | An audit trail is the only after-the-fact answer to "who did what," and it is worthless if it cannot be trusted not to contain secrets |
| Start-up ordering | The facade refuses to serve traffic before its dependencies — legacy system readiness, client registration, schema — are confirmed ready | Otherwise the first requests after a deploy fail in ways that look like application bugs |
| Input limits | Every field bounded to what the legacy schema can actually hold, as a guard — not as a policy statement | A guard stops absurd input; a policy at login locks out real accounts that predate the policy |
| Attack surface | Auth surface nothing calls (old grant types, old auth schemes) is removed, not left live-but-unused | Unused auth surface has no owner, no monitoring, and no maintainer |
| Open redirect | Any post-login or post-logout "return here" parameter validated against a host allow-list, with a safe default | An unvalidated redirect is a phishing primitive handed out by your own login page |

## 15. Setup, configuration, and operational decision points

Smaller, implementation-level choices that recur in almost any facade,
regardless of stack. These rarely get their own design discussion and
rarely get written down, which is why they are collected here.

### 15.1 Configuration and options

- **Decide, per setting, whether "absent" means "broken" or "safe," and
  validate accordingly.** Do not apply one blanket policy to every option
  class. Required connection details should be validated at startup so a
  bad deploy crashes immediately rather than on the first request. But an
  admin-revocation allow-list that is empty is a *valid, safe* default —
  nobody can revoke anyone, which fails closed — not a misconfiguration,
  and validating it as required would block a legitimate deployment.
- **Let the absence of a configuration section select a safe fallback,
  instead of a separate feature flag or build configuration.** A database
  or certificate configuration that is "fully present or fully absent" can
  select, at runtime, between real infrastructure and a throwaway local
  fallback — an in-process database, self-generated development
  certificates. One codebase serves both a developer's machine and real
  infrastructure, chosen by what configuration actually exists rather than
  by an `isDevelopment()` branch scattered through feature code. Note the
  consequence for testing: this is two code paths, and the fallback is the
  one every test hits by default (§16.3).
- **Enforce required secrets at the orchestration layer, not only in app
  code.** A deployment tool that refuses to start the stack at all when a
  required secret is unset — naming exactly what to set — is far cheaper to
  diagnose than a container that starts, binds configuration, and then
  throws deep in a stack trace.
- **Default every environment-sensitivity switch to the conservative
  setting**, with explicit opt-in to anything looser. Default the
  environment to production-like for every service, and gate any
  development-only surface — an API document, an interactive schema
  explorer — behind an explicit development flag in code. A forgotten
  environment variable then fails toward *less* exposure, not more.

### 15.2 Secrets and certificates

- **Size secret protection to the actual threat model, not uniformly.** A
  certificate that protects only local, at-rest confidentiality on a single
  machine does not need the same passphrase discipline as a secret crossing
  a real trust boundary. A database password or a client secret is still
  required and still enforced at orchestration time. Decide per artifact
  what it actually protects against rather than reflexively encrypting
  everything with a passphrase.
- **Anything that must survive a restart cannot live only in process memory
  or a per-process temporary file.** Audit every "generated fresh at
  startup" artifact — a correlation store, a signing certificate, a key
  ring — for this exact failure mode before calling a design restart-safe.
  In §5's migration, both the protocol library's state store (a
  process-unique temporary database file) and its encryption and signing
  certificates (regenerated on every boot) were silently broken by any
  restart, and both looked fine in configuration review.
- **Generate secrets and development certificates with scripts; never
  commit them.** Setup tooling should produce the local environment file
  and certificate material on demand. Your setup documentation should name
  the exact command that produces each secret, not just "set this variable."

### 15.3 Startup and bootstrap sequencing

- **Readiness has two layers, and they fail differently on purpose.** The
  orchestrator's own health check covers the common case: do not start the
  dependent until the dependency reports healthy. The app's *own* internal
  poll of the same readiness endpoint is a second line of defence for when
  it runs outside the orchestrator — a bare local run, a race — and it
  should be **fail-open**: if the dependency never reports ready within the
  deadline, log a warning and start anyway. The reasoning: a request that
  then fails fails no worse than it would have without the check, and a
  host that never starts is worse than one that started slow.
- **Bootstrap and provisioning is its own idempotent, one-shot step — never
  folded into app startup, and never assumed to have run.** Client
  registration and database creation belong in separate one-shot steps that
  **upsert** rather than blindly insert, so running them again is safe and
  actually repairs a changed value. They should poll their own dependency
  with a bounded retry count and **exit non-zero on timeout** — the
  opposite failure mode from the app's readiness gate, because nothing
  should proceed on half-finished provisioning. Then declare a hard
  dependency so starting the app always pulls provisioning in too. Without
  that hard dependency, changing a registered value — a redirect URI —
  silently leaves a stale registration in place until somebody remembers to
  re-run the provisioning step by hand, which is a failure that presents as
  an authentication bug.

### 15.4 Request pipeline and middleware ordering

- **Order security middleware deliberately and write down why, because the
  order is itself a security property.** Forwarded-header resolution must
  run before the rate limiter and before authentication, since both depend
  on seeing the request's real scheme and client address rather than the
  proxy hop's. Rate limiting must run before authentication, so an
  over-budget request is rejected before any handler — or any call to the
  legacy system — runs at all.
- **Verify a security-critical framework feature actually behaves as
  documented on your runtime before depending on it; hand-roll the minimal
  check if it does not.** Build an isolated reproduction outside your
  codebase. If the feature does not hold up, a small, directly auditable
  middleware doing the one check is better than depending on the behaviour
  being fixed later. This generalises well past forwarded headers: trust
  nothing security-critical from a library without a reproduction, in any
  stack.
- **A multi-step flow gets one shared rate-limit budget, not one per
  endpoint.** Splitting a login flow's budget across its steps silently
  doubles it.
- **Decide queue-versus-reject explicitly for rate-limited auth
  endpoints.** Rejecting immediately over budget is usually right for a
  login flow, where queueing a flood of attempts just delays the same
  outcome. Another class of endpoint might reasonably choose to queue. Make
  the choice; do not inherit the library default silently.

### 15.5 Resilience primitives

- **A circuit breaker must be a long-lived, shared instance per guarded
  dependency — never constructed fresh per call.** Its entire value is
  state that spans requests, so a breaker rebuilt on every call can never
  trip. This is the single easiest way to ship a circuit breaker that does
  nothing, and it passes code review comfortably because the wrapping code
  looks correct.

### 15.6 Network topology and addressing

- **Pin the internal network topology when a security control depends on
  recognising it.** If a trusted-proxy check parses an incoming address
  against a configured range, that range is only meaningful if the network
  is stable — so fix the subnet rather than taking the orchestrator's
  dynamically assigned default.
- **Bind every exposed port to loopback by default; require explicit opt-in
  to bind wider.** A reusable default for any local or single-host
  deployment, and the compensating control that makes known/shared
  development credentials acceptable (§20.1).
- **Classify every URL-shaped configuration value as internal
  (service-to-service) or external (browser-facing), explicitly.** This bug
  class recurs constantly and is worth naming up front. A service's base
  URL for internal calls, the issuer string that must match what is inside
  the token, and the redirect and post-logout URIs that a *browser* must be
  able to reach are three different values that all look like "the base
  URL." §5's migration hit each of them as a separate incident at a
  separate time before anyone named the pattern. Do not assume "the base
  URL" is single-valued.

### 15.7 Cross-service configuration agreement

- **When two independently deployed units must agree on a value and there
  is no shared-config mechanism, duplicate the computation explicitly and
  say why** rather than building a shared-config system prematurely. A
  provisioning step and the app itself may each need to compute the same
  redirect URI, and if the orchestrator does not share one service's
  environment with another, computing it twice with a comment explaining
  the duplication is the honest answer. This is a deliberate, documented
  duplication — not a repetition to "fix" by introducing shared config
  ahead of an actual need.
- **Know and document the far side's exact-match semantics before assuming
  "close enough."** Any value two systems must agree on needs this question
  answered explicitly: string equality, host match, or allow-list prefix?
  The example in §8, question 4 — a post-logout redirect compared with
  plain SQL equality, so a trailing slash broke logout with no error
  surfaced to anyone — is the generic shape of this failure.

### 15.8 Data lifecycle

- **Decide the schema-creation strategy deliberately, and know when to
  graduate off it.** Creating the facade's own session schema
  automatically at startup is appropriate while that schema is small and
  still moving. Know that this is a conscious simplification to be replaced
  with versioned, reviewed migrations and a tested rollback path before
  anything resembling a production rollout (§24) — not an oversight, and
  not a permanent answer.

## 16. Testing strategy

The central difficulty in testing a facade is that the thing it integrates
with — the legacy system — is slow to start, awkward to control, and not
yours to change. That pushes teams toward mocking it entirely, which then
hides the exact bugs a facade is most likely to have: protocol mismatches,
and configuration drift between what you registered and what the legacy
system believes you registered.

The resolution is layering: a fast, in-process **fake that genuinely speaks
the protocol** for most tests, plus a small number of tests against the
real legacy system for the things only it can confirm.

### 16.1 The layers, and what each one alone can prove

| Layer | What it checks | What it cannot catch |
|---|---|---|
| Architecture tests | The compiled/structural reference graph matches the intended layering — e.g. the configuration-binding layer never references the web framework | Anything about runtime behaviour. This is a structure check, not a behaviour check |
| Unit / in-process tests | Protocol-level correctness — token exchange, signature/nonce/issuer validation, session persistence, resilience thresholds, redaction — against a real-but-fake legacy identity provider, in-process, fast and parallelisable | Anything about the *actual* legacy system's behaviour. It only ever proves the code handles what the fake sends |
| Black-box integration tests | The real legacy system, with the real registered client, actually accepts what the facade sends: redirect URI, client secret, discovery document shape | Only what is exercised. State the suite's boundary explicitly |
| Shadow comparison (pre-cutover) | Legacy and new code paths produce equivalent outcomes for the same scenario | Nothing by itself — it is a gate before cutover, not a substitute for the layers above |

A rule falls out of that table: **an architectural rule you can state in one
sentence should be an automated test, not a comment or a code-review
checklist item.** Inspect the compiled or structural reference graph, so a
forbidden reference fails the build the moment it is introduced — not
whenever somebody happens to exercise that path.

### 16.2 Testing the legacy-system integration itself

- **Stand up a real, minimal, protocol-speaking fake server for the legacy
  identity provider; do not mock the HTTP client.** Run an actual server on
  a random loopback port, implementing just enough of the token endpoint,
  the key-set endpoint, and one protected route, and drive your *real*
  protocol library against it end to end. This catches protocol-shape bugs
  a mocked client hides entirely. Building exactly this fake in §5's
  migration surfaced that the protocol library rejects an identity token
  with no issued-at claim — a requirement nobody would ever have thought to
  make a mock enforce. **When the thing under test is protocol compliance,
  fake the protocol, not your own interface to it.**
- **Prove the legacy system accepts your registration by asking the legacy
  system directly**, not by reading back your own configuration and
  trusting that it matches. Send a real authorize request, and a
  deliberately invalid token request, with the registered client's
  credentials, and check the legacy system's own response. A redirect URI
  or secret that drifted out of sync — after moving the public address
  behind a gateway, say — then fails here with an explicit invalid-client
  error, before any human notices it through a browser.
- **For every resilience threshold, test the negative space as explicitly
  as the positive case.** Do not only prove repeated server errors open a
  circuit breaker. Also prove that an ordinary rejection — a refused grant,
  a not-found from a proxied call — never contributes toward opening it,
  however many times it happens. The second test is the one that catches a
  wrong failure predicate.
- **Make slow-by-design thresholds fast in tests through the same
  configuration surface production uses**, not a separate test-only code
  path. Override circuit-breaker and rate-limit windows to a handful of
  calls and seconds via the same options binding production reads, so the
  test exercises the real decision logic on a compressed timeline rather
  than a hand-rolled shortcut that bypasses it.

### 16.3 Testing security-sensitive guarantees

- **Test a "never happens" claim at the most permissive configuration the
  system could ever run at, and guard against a vacuous pass.** If you
  claim a credential or a request body never reaches a log, test it with
  logging opened all the way to its most verbose level — that is the
  extreme a misconfigured deployment can actually reach — and **assert the
  capture mechanism itself produced some output** before asserting the
  sensitive value is absent. Without that companion assertion the test can
  pass forever because its own instrumentation quietly broke and there was
  nothing to search. Every "X must never appear" test needs it.
- **When a setting is optional-with-a-safe-fallback, test both branches
  with real artifacts**, not just that each one binds. A configuration-driven
  fork (§15.1) is two code paths, and the fallback is the one every other
  test hits by default — so the real-backend branch is the untested one
  unless you deliberately generate real artifacts (a genuine temporary
  certificate file, of the same shape your setup script produces) and drive
  a full login round trip against them.

### 16.4 Integration-test readiness fixtures

- **Poll every independent readiness condition concurrently, under one
  shared deadline, with its own per-check timeout** — not a single serial
  chain of waits. Check the database, the provisioning step's result, the
  legacy system's probe, and your own health endpoint all at once each
  round, so one slow check does not starve the deadline budget for the
  others.
- **Distinguish "this will never pass, stop polling" from "not ready yet,
  keep polling," and fail loudly and specifically on the former.** A
  database access-denied error should short-circuit immediately with a
  message naming the likely cause — a password mismatch against an existing
  volume — rather than burning the full deadline. This is the same
  fail-fast-versus-fail-open distinction as §15.3's startup gates, applied
  to the test harness instead of the app.
- **Serialise tests that share one expensive, stateful external resource;
  keep everything else parallel.** Integration tests against a live stack
  should share one collection, run one after another, with one readiness
  wait up front. Unit tests against an in-process fake should start an
  isolated instance per test and run freely in parallel. Do not let one
  shared live dependency force the whole suite serial.

### 16.5 Test-scope discipline

- **State each test layer's actual boundary where a future reader will see
  it** — in the test class itself, not only in a design document. If an
  integration suite does not exercise the login flow, say so there, so
  nobody reads a green run on it as proof that login works.
- **When a feature is removed, decide what happens to its test coverage
  explicitly.** Delete the tests that tested it rather than disabling them,
  and name any coverage gap the removal leaves behind (Phase 6.3). A
  shrinking test suite should be a decision with a stated consequence — the
  same discipline as the risk ledger.

## 17. Security testing

§16 covers test *layers*. This is the attacker's-eye cut through the same
suite: the tests that exist specifically because somebody asked "what if a
caller lies here?" Each pattern is a category to apply to any facade, not
just the endpoint it was first found guarding.

### 17.1 Forgery and replay of the identity assertion

- **Reject a correctly signed, correctly issued, correctly audienced token
  that fails on exactly one remaining check — and test that specific
  combination, not just "a bad token."** Use a genuinely valid identity
  token, real signature, right issuer, right audience, with only the nonce
  wrong. A token invalid in every dimension at once would never prove that
  the nonce check is what did the rejecting. For every independent
  validation check — issuer, audience, signature, nonce, expiry — the test
  that matters is "everything else valid, only this one wrong." It is the
  only version that proves *that* check exists rather than that *some*
  check exists.
- **Prove a rejected grant degrades to a generic error, never the
  dependency's own error text.** A rejected exchange should surface as your
  own opaque login-failed outcome, never the legacy system's response body
  — this is information disclosure as much as user experience, since an
  attacker probing a login learns which part of the attempt failed.

### 17.2 Trust-boundary and spoofing tests

- **Test the trust boundary from both sides, not just the "it works"
  side.** Pair a forwarded header honoured from inside the trusted network
  with the same header, same claimed value, **ignored** from outside it —
  only the caller's network membership changed. A suite that tests only the
  trusted path cannot distinguish "the trust check works" from "the header
  is always honoured." Test the un-forwarded default too.
- **Prove the spoofing defence actually changes security-relevant
  behaviour**, not just an internal flag nothing reads. Assert on a
  consequence: a cookie's secure flag being set behind the trusted proxy,
  or the rate limiter keying on the real client address.
- **For any security-relevant middleware driven by configuration — a
  trusted address range, an allowed-host list, a signing key path — test
  that bad configuration fails the host at startup**, never falls back to
  an open or permissive default. An unparseable or absent trusted-network
  setting must crash the host rather than silently trusting every caller.
  The generalisable shape: *invalid* security configuration is a startup
  test with an expected exception, exactly as much as *valid* configuration
  is a behaviour test.

### 17.3 Session security and privilege-boundary tests

- **Prove revocation kills the session server-side, from a cookie copy the
  revoking call never touched.** Log in as an independent browser with its
  own cookie jar, revoke, then re-assert against that same independent
  session. The threat model for revocation is specifically "a copy of this
  session exists somewhere I do not control," and a test that only checks
  the revoking caller's own next request cannot distinguish real
  server-side revocation from a client-side no-op.
- **Test blast radius in both directions for any action affecting multiple
  sessions: it must reach every session it should, and none it should
  not.** These are a matched pair — one guards under-reach (an attacker's
  second session surviving a revoke-all the legitimate user triggered), the
  other guards over-reach (one identity's action silently affecting
  another's). Write both halves for every bulk action the facade exposes.
- **Test the negative case of a privilege boundary as thoroughly as the
  positive case, and assert the attempt changed nothing.** Do not stop at
  asserting a forbidden status; re-check that the target's session is still
  alive, proving the rejected attempt had zero side effects rather than a
  refused response wrapped around a side effect that happened anyway.
- **Test the self-targeting edge case of any elevated action separately.**
  An admin revoking their own identity should be signed out too. "Can
  affect anyone" and "can affect anyone, including themselves" are easy to
  get different answers to by accident — a caller-exclusion check added for
  some unrelated reason is enough to diverge them.
- **Every session-mutating or identity-revealing endpoint needs its own
  "no session, no access" test**, not an inherited assumption from another
  endpoint's coverage. Even when several routes ultimately hit the same
  authorization middleware, middleware wiring regresses per-route often
  enough to justify the duplication — in §5's migration a policy change
  once silently stopped authenticating the cookie scheme at all, and only a
  per-route assertion would have caught it.

### 17.4 Abuse and rate-limit boundary tests

- **Test the exact boundary — N passes, N+1 is refused** — not "a lot of
  requests get refused eventually." Send exactly the configured limit,
  assert none were refused, then assert the very next one is. That proves
  the limit is the configured number rather than approximately it.
- **Test that a shared budget across a multi-step flow cannot be doubled by
  alternating between its steps.** Interleave the login-start and callback
  calls from one address and confirm the shared budget exhausts at the
  combined total. This is the test that catches a regression back to two
  independent per-endpoint limiters, which is an easy mistake the moment a
  second endpoint joins an existing flow.
- **Test that the limiter does *not* apply where it should not**, as
  explicitly as proving it applies where it should. Call a high-traffic
  authenticated endpoint more times than the login limit allows and assert
  none are refused. This guards the opposite bug: a policy accidentally
  applied too broadly, silently breaking a legitimate high-frequency
  caller.
- **Apply the "bad configuration fails startup" rule to rate-limit settings
  too** — a permit limit below one, a window under a second. These
  degenerate configurations would otherwise either lock out every caller or
  rate-limit nothing at all.

### 17.5 Input-boundary tests: audit for the gap, do not assume coverage

Periodically grep for every security-relevant validator or guard in the
codebase — an open-redirect allow-list, a host or scheme check, an
input-length guard — and confirm each one has a test that actually drives
its **rejection** path.

This is worth doing as a deliberate sweep rather than trusting coverage
numbers, because the gap it finds is invisible by construction. In §5's
migration the open-redirect guard on the login endpoint's return-URL
parameter had no dedicated test at all: the code was short, looked
obviously correct on inspection, and nothing else in the suite happened to
exercise its rejection branch. "The code is simple" is not evidence that it
is tested. The generalisable lesson is the sweep, not the specific gap.

### 17.6 Fail-closed configuration, as a cross-cutting category

Collecting the pattern that recurs in §17.2 and §17.4: **any option
controlling a security decision — a trusted network, a rate limit, an admin
allow-list, a certificate path — should have a startup test proving that
invalid or degenerate values refuse to start the host**, not just tests
proving valid values work.

It is the same test shape reapplied per option class. When adding a new
security-relevant setting to any facade, add its fail-closed test in the
same change, not as follow-up.

## 18. Logging, auditing, and monitoring

Two different needs get conflated here, so separate them up front. An
**audit trail** answers "who did what, when" after the fact, for security
and compliance — it must be complete, trustworthy, and free of anything
sensitive. **Monitoring** answers "is this healthy right now" — it needs
numbers over time, not individual records. A facade needs both, and
structured logs alone give you only the first.

### 18.1 Structured, stable-category audit logging

- **Give the audit trail its own fixed, stable logger category,
  independent of which file or module the logging call happens to live
  in.** The practical trick is a dedicated marker type or constant that
  exists only to pin the category name, so a refactor that moves an audit
  call to a different file cannot silently rename the category that a log
  pipeline, a search query, or an alert is filtering on. Treat audit
  category names as a published contract with whatever consumes them.
- **Use structured, strongly typed log methods for anything queried or
  alerted on**, not ad hoc string interpolation. Every audit event should
  have a fixed message template and named, typed fields — subject, reason,
  method, path, status — that a log backend can filter and aggregate
  directly rather than parsing out of a free-form string.
- **Scope the audit trail to where data actually moves, and say why
  something in scope was deliberately left out.** Cover login outcomes,
  proxied data access, and admin actions. A high-volume, low-sensitivity
  endpoint can be a deliberate, stated exclusion: §5's migration
  deliberately did not audit its identity-check endpoint, on the grounds
  that it returns no sensitive data and fires on every page load, making it
  too high-volume to be worth the noise for what it would add. That is a
  real tradeoff — logging volume against per-event signal — made
  explicitly, rather than either logging everything or logging arbitrarily.
- **Never log a reason string that could leak the dependency's own error
  detail.** Use a short, fixed internal label. This is the same discipline
  as §17.1's generic error response, applied to what reaches the log rather
  than what reaches the browser — a log reader may be a different trust
  tier than whoever should see the legacy system's internal errors.
- **Redaction is a tested guarantee, not a logging-policy statement.** The
  design here and the test in §16.3 are two different artifacts, and a
  facade needs both. A policy that says "we never log tokens" and no test
  asserting it is a policy that will be true until somebody adds a
  convenient debug line.

### 18.2 Liveness versus readiness: know which one you actually have

- **A health endpoint that always returns success answers "is the process
  up," and nothing else.** It says nothing about whether the process can
  actually serve a login — reach the legacy system, reach its own session
  store. That is a legitimate endpoint to have; the mistake is letting its
  name imply more. Know, for every health or probe endpoint in the system,
  which of the two questions it actually answers, and do not let an
  orchestrator's liveness probe quietly stand in for a readiness probe. A
  process that is "up" but cannot reach its session database should fail
  *readiness*, or a load balancer keeps sending it real traffic it cannot
  serve.
- **A production deployment needs a readiness endpoint that actually checks
  its own critical dependencies** — at minimum the session store. §5's
  migration had only a liveness endpoint and treated the *legacy system's*
  readiness probe as real readiness at startup, which is not the same
  thing and was named as a gap rather than assumed covered.

### 18.3 What observability needs beyond structured logs

Structured logs and a liveness check are not the observability a real
production deployment needs, and conflating them is exactly how a facade
ships without anyone knowing it is slow or failing until a user reports it.
§5's migration deferred this category explicitly — "metrics and tracing
come later" — which is an honest baseline for an early-stage facade and a
blocker for a production one.

Add, deliberately:

- **Latency and error-rate metrics per outbound dependency call**, broken
  out individually: the token exchange, the key-set fetch, and each proxied
  call — the same three call sites §14 already requires a circuit breaker
  around. A breaker trips on a *ratio*; you cannot tune that ratio, or
  notice it drifting toward trouble, without the latency and error metric
  underneath it.
- **Circuit-breaker state transitions as a first-class signal**, not just
  the generic failure a caller sees. Alert on *open* — something is
  actually wrong with the legacy system — and log or measure *half-open*
  probe outcomes. The facade's own resilience layer is one of the
  highest-signal indicators of legacy-system health available anywhere, and
  it is wasted if only the breaker itself knows it tripped.
- **Rate-limit rejection counts as both a security and a capacity
  signal**, fed somewhere alerting can see. A spike is either an attack
  (§17.4's tests) or a legitimate traffic pattern the configured limit no
  longer fits — the metric is what tells you which, instead of users
  reporting refusals anecdotally.
- **Session-store latency**, once a server-side session store exists
  (§9.2). It sits on the critical path of every authenticated request
  through the facade, not just login, so it deserves the same attention as
  the legacy system's own latency.
- **Correlation and trace IDs propagated across every hop** — gateway to
  facade to legacy system, and back again for the response — so a slow or
  failing request can actually be followed across the boundary this whole
  handbook exists to bridge. Without it, every incident investigation
  starts by manually correlating timestamps across two systems' separately
  formatted logs.

### 18.4 Applying this section

Use §18.1's pattern — stable category names, structured fields, explicit
in-and-out-of-scope decisions, redaction discipline — for every new
security-relevant log you add. Use §18.2 to classify every health or probe
endpoint explicitly as liveness or readiness. Treat §18.3 as the checklist
of what must exist before "it has structured logs" can be mistaken for "it
is observable in production," and fold its items into your own production
gate (§24).

## 19. CI/CD

A facade is unusually sensitive to *configuration* rather than just code,
which means a pipeline that builds and unit-tests the code but runs it in a
configuration nobody will ever deploy misses the most likely class of
failure. The theme of this section: **make the pipeline run the real thing,
configured the real way.**

### 19.1 Test what people actually run

- **Build the pipeline's configuration from the same template a developer
  or a deployment uses**, overriding only secret values. Do not maintain a
  second, looser pipeline-only configuration file. The direct consequence
  is worth stating: the pipeline then runs the facade in its production-like
  mode, loopback-bound, non-root, exactly as a real deployment would — so a
  configuration-driven security regression, in a setting that is only safe
  because the conservative mode is the default (§15.1), gets caught in the
  pipeline instead of surfacing for the first time in a real environment.
  **A pipeline more permissive than production, by convenience, is testing
  a system that does not exist.**
- **Run the real orchestrated multi-service stack, not a stubbed-down
  subset.** Bring up the database, the legacy system, the provisioning
  steps, and the facade itself, and run the integration layer from §16.1
  that proves the legacy system genuinely accepts your registration — not a
  mock standing in because "the pipeline is slow, so mock it there." If the
  pipeline is slow because the legacy system boots slowly, §21 is the fix,
  not mocking.

### 19.2 Secrets in the pipeline

- **Fall back to a known-fake secret when a real one is not configured, and
  draw from the secret store first.** A fork, or a run without repository
  secrets configured, still gets a deterministic, throwaway credential
  rather than an empty string that fails in some confusing downstream way.
  This only works because nothing downstream treats "set to a known-fake
  value" as equivalent to "unset" (§15.1's orchestration-level enforcement)
  — the two failure modes must stay distinguishable.
- **Never let the pipeline echo a secret value, even for debugging.** If
  you need to confirm the environment is populated, print variable *names*
  only. A pipeline log is frequently a lower-trust-tier artifact than the
  secret store feeding it: visible to more people, retained longer,
  sometimes cached.

### 19.3 Generated-artifact drift as its own gate

- **Anything committed but generated gets a step that regenerates it and
  diffs against the commit** — not just trust in whoever ran the generator
  last. A committed API client, a schema snapshot, a generated API
  document: regenerate from a live instance and fail the build on any
  difference, so an endpoint changed without re-running the generator
  cannot merge.
- **Order that check correctly relative to the stack's own startup
  dependencies.** A regeneration step that drives a running instance needs
  the same readiness the app itself needs, so it must run *after* the stack
  is up, not before. Pipeline step ordering should mirror the runtime
  dependency graph; get it backwards and the check fails for the wrong
  reason every single time.

### 19.4 Readiness and teardown as pipeline citizens

- **Reuse the orchestrator's own health checks as the wait condition**
  rather than reinventing polling in pipeline script. One readiness
  definition then serves local development and the pipeline alike, instead
  of a pipeline-only wait loop that drifts from what the health check
  actually checks.
- **Give a slow-starting dependency a generous, explicit wait timeout,
  bounded by an overall job timeout.** A slow-but-working first boot is not
  penalised, while a truly hung run still gets killed rather than burning
  the runner indefinitely. And once §21 has made startup fast and
  predictable, **come back and shorten this** — a generous timeout over a
  fast operation is not safety, it is a delay on every genuine failure.
- **Add a dedicated, best-effort diagnostics step that runs even when
  earlier steps failed**, separate from the actual assertions. One step
  that always runs, probing readiness endpoints and printing container
  state, leaves a readable trail; another that runs only on failure dumps
  container logs. Diagnostics and assertions are different steps with
  different conditions, not bolted together.
- **Tear the stack down unconditionally, including its volumes.** A failed
  run must not leave stale state for the next one. Every environment in
  this kind of project should be resettable, and the pipeline is where that
  is cheapest to enforce.

### 19.5 Supply chain and versioning, pinned into the pipeline

- **Pull sanitised fixture data by a pinned, versioned reference; never
  commit it to the repository.** A database dump in git history is
  re-downloaded by every clone forever, with a fresh copy added every time
  it is regenerated. Publishing it as a tagged release artifact is
  deterministic and reproducible per run without bloating the repository,
  and the tag itself documents exactly which legacy-system version the
  fixture was captured against (§21).
- **Re-apply fresh, run-specific credentials onto a restored static
  fixture** rather than baking credentials into the fixture. The same
  artifact is then reusable indefinitely without ever needing its own
  secret rotated (§21.4).
- **Pin the toolchain version from one file both the pipeline and local
  development read.** One source of truth for the compiler or SDK version
  avoids the class of bug where the pipeline silently uses a different
  version than a developer's machine resolves.

### 19.6 Pipeline hygiene

- **Grant the pipeline's credentials the minimum scope they need.** A job
  that checks out code and downloads a release asset has no business with
  write access to anything.
- **Cancel superseded runs instead of letting them queue.** This matters
  specifically for an expensive pipeline — standing up a full
  multi-container legacy stack per run — where three rapid pushes to one
  branch queuing three full boot-and-test cycles is pure waste.
- **Upload structured test results as a build artifact, not just console
  output**, and make that upload tolerant of there being no results at all,
  so it never fails the build just because a genuinely broken earlier step
  produced nothing to upload. A partial or failed run should still leave an
  inspectable, durable record.

## 20. Containers and orchestration

Nothing in this section is specific to authentication. But a facade is a
security-sensitive, internet-facing component that talks to a legacy system
you often cannot modify, which makes it exactly the place where sloppy
container defaults are most costly. If you are already fluent in container
hardening, skim for §20.4 (dependency conditions) and §20.5 (root), the two
most commonly gotten wrong in this specific shape of system.

### 20.1 Image-level choices

- **Multi-stage builds: a full toolchain to compile, a minimal runtime to
  ship.** Ship the smallest image that can actually run the artifact, not
  the image that built it. A compiled static-asset frontend may need no
  language runtime at all — just a plain web server.
- **Order build steps for layer-cache reuse:** copy manifest and lock files
  and install dependencies *before* copying application source, in any
  language. A source change then invalidates only the final layer, not the
  slow dependency-resolution layer.
- **Run as the image's non-root user by default; know why before overriding
  it.** Many official base runtime images ship a built-in unprivileged user,
  so this is one line in the runtime stage. See §20.5 for the detail —
  including why the build stage running as root is fine.
- **A port-exposure declaration documents intent; it does not create a
  listener.** The real bind configuration lives in whatever environment
  variable, code, or orchestration setting actually binds the port. Declaring
  a port the process never listens on is a readable comment, nothing more.
- **Pin runtime and base images by digest, not just by tag — and state any
  deliberate exception explicitly.** There is a legitimate exception worth
  naming: if you hold a captured fixture of the legacy system's installed
  state (§21), that fixture is only valid for the version that produced it,
  so pinning *that* image by version tag makes the tag itself the
  compatibility constraint rather than a convenience. Every deviation from
  pin-by-digest should be that explicit, never silent.
- **Mount read-only wherever the container only needs to read** — secrets,
  certificates, provisioning scripts. A container compromised through the
  app it runs then cannot use that foothold to modify the secret material,
  or the provisioning script that produced its own configuration.
- **Choose a restart policy per container role, not uniformly.**
  Long-running services restart automatically. One-shot provisioning
  containers must not: a provisioning container that restarts on a
  successful exit either loops forever or masks a real failure as a
  crash-restart.
- **State plainly when local or pipeline credentials are deliberately known
  and shared**, rather than letting that fact hide in an example config
  file, and compensate with network defaults — loopback-only binding
  (§15.6) — rather than secrecy. That is the right call for a disposable
  stack, and a decision that must flip, to real secrets with real rotation,
  the moment the stack stops being disposable.
- **A health check should probe actual readiness, not just that the process
  started** (§18.2). A command-based check hitting a real readiness
  endpoint catches the "container running, application not yet serving" gap
  that a bare port-open check misses entirely.
- **A service's build context must extend to cover every shared code
  reference, even across directory boundaries.** A frontend that references
  a generated client living under a different service's directory needs a
  build context wide enough to include it — commonly the repository root
  rather than its own subdirectory. Any monorepo with generated or shared
  clients hits this as soon as the consuming service is containerised
  separately from the one that owns the API it calls.

### 20.2 Container networking: two addresses for every service, on purpose

An orchestrator's embedded DNS resolves service names for
container-to-container traffic; published ports are for the host, or a
browser, only. **A facade touches both simultaneously** — it calls the
legacy system internally by service name, while a browser must reach the
gateway at a published, host-reachable address.

This is the mechanism behind §15.6's internal-versus-external rule. Know,
for every container-to-container call, whether it is an internal hop (a
service name, never a published port) or something an external caller must
resolve (a published address). The two are never interchangeable, and
getting it backwards is a recurring bug class in exactly this shape of
system — it accounted for three separate incidents in §5's migration before
anyone named the pattern.

Pin the orchestrator's network topology — a fixed subnet rather than a
per-project dynamic default — whenever container-level logic parses an
incoming address against it. A trusted-proxy check is only meaningful if
the network it checks against is stable.

### 20.3 Volume types: match the mount to what the data actually needs

| Data | Mount type | Mode | Why |
|---|---|---|---|
| The database's own files | Named/managed volume | read-write | Must outlive container recreation; no reason to be visible as plain host files |
| The legacy system's own writable site data and key material | Named/managed volume | read-write | Same durability need, and often shared read-write with a provisioning container that needs the installation's keys |
| Generated certificates | Bind mount | **read-only** | Host-authored input (§15.2); the running container only ever reads it |
| Provisioning scripts | Bind mount | **read-only** | A compromised provisioning container gaining write access to its own script would be a privilege-escalation path for nothing it legitimately needs |

The pattern: **named volumes for state the container itself owns and
mutates; read-only bind mounts for anything the host hands the container as
input.** Getting it backwards — a bind-mounted database directory, or a
writable mount for a provisioning script — either loses durability or
widens what a compromised container can modify.

### 20.4 Dependency conditions are not one thing — pick deliberately

An orchestrator's "depends on" relationship usually comes in at least three
flavours, and this kind of stack typically needs all three:

- **Wait for the dependency's own health check**, not merely for its
  process to start. Appropriate whenever the dependent would fail outright
  against a dependency that is "running" but not yet actually serving — the
  facade against the legacy system, the legacy system against its database.
- **Wait for a one-shot container to *finish***, not to become healthy — it
  has no long-running health to check. This is the concrete mechanism
  behind §15.3's "bootstrap is its own idempotent step, never assumed to
  have run": the orchestrator itself refuses to start the facade until
  provisioning has actually completed, rather than the ordering being a
  documentation promise someone can run out of sequence.
- **Start-order only, with no wait.** Appropriate only when the dependent
  can tolerate the dependency not being ready — a gateway in front of
  backends, where its own requests have retry-worthy HTTP semantics and the
  facade's circuit breakers already cover the gap. **Using this form where a
  real readiness dependency exists is the mistake to watch for:** it often
  looks identical in configuration to the health-gated form, differing by
  one easy-to-miss keyword.

### 20.5 Root considerations, in depth

§20.1 states the rule. This is why, where it applies, and where it does
not — root-in-container is easy to think of as one on/off switch when it is
actually several separate decisions.

- **What a built-in non-root user actually is.** Many official base runtime
  images ship an unprivileged user baked into the image itself, exposed as
  a build argument the image defines. Switching to it is one line, in the
  **runtime** stage only. The build stage above it still running as root is
  normal and low-risk, because nothing from that stage ships — only the
  published output is copied forward.
- **Binding to a privileged port is the classic reason a container "needs"
  root, and the fix is to never use one.** A non-root process cannot
  normally bind a port below 1024 without an elevated network capability.
  Listen on a high port inside the container and let the orchestrator's own
  port publishing do the 80/443-shaped remapping at the host level, which
  needs no privilege inside the container at all. Picking high ports inside
  the container is precisely what makes "just run as non-root" possible
  without granting a capability back. Know this before accepting that some
  future service "needs" a privileged port: it almost never does, once the
  remapping happens outside the container instead of inside it.
- **Read-only bind mounts sidestep the usual non-root permission
  friction.** A non-root container process writing to a bind-mounted host
  directory commonly hits a user/group ID mismatch. Making every bind mount
  read-only (§20.3) avoids the problem entirely rather than solving it,
  because nothing mounted that way is ever written to from inside a
  container.
- **Non-root discipline applies to images you *build*; it does not reach
  into images you only *consume*.** Your own services can each set a
  non-root user explicitly. A third-party database image, the legacy
  system's own vendored image, an administration UI — each runs as whatever
  its upstream Dockerfile defaults to, commonly root for initialisation,
  sometimes dropping privileges before the main process starts, entirely at
  that image's discretion. **A "we run non-root" claim needs to specify
  which containers it actually covers.** It is a per-image property, not a
  property of the stack, and the legacy system's own image is usually the
  container you have least control over.
- **A one-shot provisioning container is a different risk profile than a
  long-running root process**, even when both technically run as root. One
  exists to run a script to completion and exit; the other stays up
  accepting requests for the life of the container. Do not apply the same
  scrutiny uniformly.
- **Further hardening beyond "non-root,"** each a real step worth
  evaluating per service before production: disallow privilege escalation,
  drop all Linux capabilities and add back only what is actually needed,
  and run with a read-only root filesystem with explicit writable mounts
  for the few paths that need them. A non-root process can still do damage
  with an unneeded capability or a writable filesystem it does not need.

### 20.6 Two gaps worth naming explicitly

Following §17.5's practice of naming an absence plainly rather than
implying full coverage — these two are commonly missing, and were missing
in §5's migration:

- **Resource limits.** It is easy to set no CPU or memory limit anywhere in
  a local or pipeline stack. That is a reasonable simplification there; it
  is explicitly **not** acceptable at §24's production bar, and getting
  from here to there means setting those limits deliberately from a real
  load test, not inheriting "no limit" as though it were a tested default.
- **Container image vulnerability scanning.** A pipeline that builds and
  runs every image but never scans one is easy to mistake for a pipeline
  that verifies them. This is a short, obviously addable step, not a design
  decision — name it as a gap if it is not there yet.

### 20.7 Useful orchestration commands for this shape of stack

```bash
# Validate and print the fully resolved config (variable substitution,
# file merges) without starting anything.
docker compose config

# Bring the stack up, blocking until every service's own healthcheck — or
# successful completion, for one-shot provisioning containers — passes.
# This is the same condition the pipeline's start step should rely on.
docker compose up -d --wait --wait-timeout 1200

# Bring up just one service and its dependency chain, for iterating on one
# container without rebuilding or restarting everything else.
docker compose up -d --build facade

# Tail logs for one service, or for everything, live.
docker compose logs -f facade
docker compose logs -f

# Each service's current health and state at a glance.
docker compose ps

# Run a one-off command inside a running container (a shell, a bundled
# client) without modifying the image.
docker compose exec facade /bin/sh

# Re-run a one-shot provisioning container by name — needed when the
# orchestrator's own change-detection does not trigger it, which is the
# exact gap §15.3's hard dependency exists to close.
docker compose up seed

# Tear down and remove volumes too: the same unconditional cleanup the
# pipeline should do (§19.4), useful locally after changing seed data or
# schema.
docker compose down -v

# Scan a built image for known vulnerabilities (§20.6's named gap).
docker scout cves facade:latest
# or, if Docker Scout is unavailable:
trivy image facade:latest

# Reclaim local disk from stopped containers, dangling images, and unused
# build cache — safe to run between unrelated projects' stacks.
docker system df
docker system prune
```

## 21. Starting a slow legacy system fast

§19 insists the pipeline runs the real legacy system rather than a mock.
That advice is only affordable if the legacy system starts quickly, and
legacy systems characteristically do not. This section is how to get one up
in under a minute instead of twenty.

**The short version:** a legacy system's container image starts in seconds,
but its *first-run installer* takes minutes. A pipeline always starts from
an empty volume, so it pays that installer cost on every single run.
Capture the state the installer produces — the database **and** the files it
generates outside the database — publish it as a versioned artifact, and
restore that instead. Restoring is I/O-bound and predictable; installing is
CPU-bound and variable.

### 21.1 The problem

To run meaningful integration tests against a legacy system you cannot
mock, your pipeline has to actually start it: a database, the legacy
application, its one-shot provisioning steps, and only then the new
services that depend on it.

That sequence is strictly ordered. The facade cannot start until the legacy
system is genuinely ready (§15.3), and the tests cannot run until the
facade is up. So the whole pipeline serialises behind the slowest link, and
in a legacy-integration project that link is almost always the legacy
system's own startup.

You can see the problem's shape in how defensively such a stack ends up
configured. In §5's project: the legacy system's health check was given a
three-minute grace period during which failures did not even count against
its retry budget, because a fresh install legitimately took that long. The
pipeline's own start step allowed twenty minutes. The client-provisioning
script polled for the legacy system's tables to exist, retrying for up to
fifteen minutes. And the facade polled the legacy system's readiness
endpoint at startup on top of all of that.

Every one of those numbers was generous because it had to be. And that
carries a second cost beyond waiting: **when something is genuinely broken,
the pipeline takes twenty minutes to say so.** Long timeouts produce slow
failures, not just slow successes.

### 21.2 Why it takes so long

The intuitive assumption is that the container is slow to start. It is not
— the container starts in seconds. What is slow is what the application
does *the first time it finds an empty database*.

A legacy system's first-run installer typically:

1. **Creates its schema** — often hundreds of tables.
2. **Seeds reference data** — code lists, defaults, lookup tables,
   configuration rows. In a clinical system, a great many rows.
3. **Generates per-installation secrets** — site encryption keys, used to
   encrypt values it then stores in its own database.
4. **Writes configuration files** — database credentials, site paths,
   feature flags — in files *outside* the database.
5. **Runs upgrade and migration logic** — frequently version by version, in
   sequence, each step inspecting what the previous one did.

Three properties of that work make it expensive in a pipeline:

**It is CPU- and round-trip-bound, not I/O-bound.** It is not writing much
data; it is executing a long sequence of small, interdependent steps, many
of them separate database round trips. That does not parallelise and does
not benefit from a faster disk.

**It is unavoidable on a fresh volume.** This is the critical point. A
developer pays the installer cost once and then has a warm volume for
weeks. A pipeline runner is new every time — fresh volume, every run,
forever. The cost that is one-time locally is per-run in the pipeline.

**It is variable.** A cold install takes noticeably longer than a warm
restart, and runner performance fluctuates. You cannot set a tight timeout
around work whose duration you cannot predict, so you set a generous one —
which is why a hung run costs twenty minutes.

Being precise about what is and is not slow determines what is worth
capturing:

| Step | Roughly how long | Worth snapshotting? |
|---|---|---|
| Pulling the image | seconds (cached after first) | No — the registry and layer cache already handle this |
| Starting the container process | seconds | No |
| **First-run installer** | **minutes, variable** | **Yes — this is the entire problem** |
| Starting on an already-installed volume | seconds | No — this is the state you are trying to reach |

The goal, stated precisely: **reach the "already-installed volume" state
without ever running the installer.**

### 21.3 The solution: snapshot the post-install state

Run the installer **once**, by hand, on a machine where you do not mind
waiting. Capture everything it produced. Publish that as a versioned
artifact. From then on, every pipeline run and every developer's first
setup restores the artifact instead of installing.

**What has to go in the snapshot** — this is where most attempts at this
technique fail, so be exact. **A database dump alone is not enough.** The
installer generates state in at least two places, and restoring only one
leaves you with a system that starts but does not work, usually failing
later and confusingly, in a way that looks like an application bug rather
than a restore problem.

A workable snapshot is three files:

| File | What it is | Why it is needed |
|---|---|---|
| `db.sql` | Full database dump — schema, seed data, and everything the installer wrote | The obvious part: the schema and reference data you would otherwise wait for |
| `sites.tar.gz` | Archive of the legacy system's own writable directory: its configuration file **and its site encryption keys** | The non-obvious part. Without the keys, the application cannot decrypt the encrypted values sitting in the database you just restored |
| `images.txt` | The exact image list the snapshot was captured from | A snapshot is only valid for the version that produced it. This records which version that was |

**The encryption-key point deserves emphasis**, because it is the failure
hardest to diagnose. If the legacy system encrypts values with
per-installation keys stored on disk, then a database row encrypted during
capture is only readable by the keys from that same installation. Restore
the database with *fresh* keys and the data is intact but unreadable — and
the symptom is typically a feature failing deep inside the application, not
an error at restore time.

Generalising: **inventory every place the installer writes.** Database,
configuration files, key material, generated assets, upload directories.
Whatever it writes and later depends on has to be in the snapshot.

**How much this saves:**

| | Install path | Restore path |
|---|---|---|
| What happens | Hundreds of sequential schema and seed operations, key generation, migration logic | Stream a dump into the database; untar an archive into a volume |
| Nature of the work | CPU and round-trip bound | I/O bound, mostly sequential |
| Duration | Minutes, **variable** | Well under a minute, **predictable** |
| On a fresh volume | Full cost, every time | Full cost, every time — but the full cost is now small |

The headline saving is roughly **several minutes, up to twenty in the worst
case, down to under a minute**, on every pipeline run and every developer's
first setup. But the second-order benefits are arguably worth more:

- **Predictability lets you tighten timeouts.** Once startup is
  consistently fast, a generous timeout is no longer protecting you from
  normal variance — so a real hang can fail in two minutes instead of
  twenty. Fast failure is a productivity feature.
- **Every run starts from a byte-identical state.** The installer is
  deterministic in principle but sensitive to timing and environment in
  practice. A restored snapshot removes a real source of test flakiness:
  tests that pass or fail depending on what the installer happened to
  produce that time.
- **You can snapshot an *interesting* state, not just a clean one.** The
  snapshot is whatever the system looked like when you captured it. If your
  tests need a configured, populated system, capture it that way once
  rather than having every run build it up through the application's own
  slow paths.
- **Developer onboarding collapses.** One command gives a working stack in
  about a minute. Nobody's first day is spent watching an installer.

### 21.4 What makes this tricky — read before implementing

The technique is simple; three details are not.

**A snapshot freezes credentials.** Whatever passwords and secrets existed
at capture time are baked in, in several places at once: the database's own
user accounts, the application's configuration file, any secret the
application stored encrypted, and the admin account's password hash. Two
ways to handle it:

- **(a) Treat the snapshot's credentials as fixed and well-known.** Simple,
  and acceptable for a disposable local/pipeline stack — provided you state
  plainly that these are known, shared, non-secret values and compensate
  with network isolation rather than secrecy (§20.1).
- **(b) Re-apply fresh credentials after restoring.** More work, but the
  same snapshot artifact is then reusable indefinitely, by anyone, without
  ever needing its own secrets rotated.

Doing both, for different secrets, is a reasonable answer: §5's project
treated the admin password as (a), documented as a fixed known value that
must match the snapshot, while the database password and the OAuth client
secret were (b), re-applied on every restore.

**Re-applying a secret may require running the legacy system's own code.**
This is the subtlest part of the whole technique. If the legacy system
stores a secret **encrypted with its own per-installation keys**, you
cannot produce a valid replacement from outside it. Writing a plaintext
value into the column does not work: the application decrypts and compares,
so a plaintext value simply never matches. You have to encrypt the new
value *with the restored installation's own keys*, which in practice means
**running the legacy application's own crypto code against the restored key
material** — boot its bootstrap in a throwaway container, with the restored
volume mounted, and call its own encryption routine to produce the value
you then write to the database.

With a sting in the tail: the legacy application's bootstrap may itself
**query the database during startup**. So the throwaway container doing the
encryption also needs to reach the database — which means joining the
running database container's network rather than running in isolation.
Without that, the bootstrap fails before the encryption code is ever
reached. The generalisable lesson: **when re-applying secrets into a
restored snapshot, expect to run the legacy system's own code, and expect
that code to have its own environmental requirements.** Budget for it; it
is usually the longest part of implementing this technique.

**Database users may not be in the dump.** In MySQL and MariaDB, user
accounts are server-global, not part of any one database, so a
single-database dump does not contain them. Restoring the dump gives you
the data with no user able to log in to it — hence an explicit
user-creation step, *before* credentials are applied. Check the equivalent
for your engine; the general question is **"what does my dump deliberately
not include?"**

### 21.5 How to implement

Five steps. Steps 1–2 you do once, repeating only when the legacy version
changes; steps 3–5 are the automation that uses the result.

**Step 1 — Capture the snapshot.** Goal: three artifacts representing a
known-good, fully-installed state.

1. **Get the system to the state you want to freeze.** Let the installer
   run to completion, once. Then apply any configuration your tests need —
   enabled features, API settings, a registered OAuth client. Everything
   done here is work your pipeline never repeats.

2. **Stop the applications, but leave the database running.** You need the
   database up to dump it, and the applications stopped so the database and
   the file state are consistent with each other — a dump taken while the
   application is mid-write can disagree with the files you archive a
   moment later.

   ```bash
   docker compose stop legacy-app facade
   trap 'docker compose start legacy-app facade' EXIT   # restart even on failure
   ```

   That `trap` matters: a capture script that fails partway must not leave
   your stack stopped.

3. **Dump the database transactionally, to a temporary file first.**

   ```bash
   docker compose exec -T db sh -c \
     'mariadb-dump -uroot -p"$MYSQL_ROOT_PASSWORD" --databases "$DB_NAME" \
        --routines --events --single-transaction' > snapshots/db.sql.tmp
   mv snapshots/db.sql.tmp snapshots/db.sql
   ```

   Four details, each of which is a bug if you miss it:
   - `--single-transaction` for a consistent point-in-time dump.
   - `--routines --events` — stored procedures and scheduled events are
     part of the installed state and are *not* included by default.
   - **Write to a temp file and move it into place.** A failed dump must
     never overwrite a known-good snapshot.
   - Stream to the host rather than writing inside the container: a
     container writing to a path that a previous restore left root-owned
     will fail.

4. **Archive every directory the installer writes**, not just the database.

   ```bash
   docker run --rm \
     -v "stack_sitesvolume:/data" \
     -v "$PWD/snapshots:/backup" \
     alpine sh -c "tar czf /backup/sites.tar.gz -C /data . \
                   && chown $(id -u):$(id -g) /backup/sites.tar.gz"
   ```

   The `chown` is there because the container runs as root and would
   otherwise hand you an archive you cannot manage as your own user.

5. **Record the versions that produced it.**

   ```bash
   docker compose config --images > snapshots/images.txt
   ```

   A snapshot is only valid for the version that made it. This file is what
   lets you answer "is this snapshot still the right one?" later.

**Done when:** the three artifacts exist, and your stack is running again.

**Step 2 — Check it, then publish it as a versioned artifact.**

1. **Verify it contains no real data.** A snapshot of a running system
   contains whatever was in that system. Make this an explicit, deliberate
   check before the artifact goes anywhere — synthetic or properly
   sanitised data only.

2. **Do not commit it to the repository.** A dump of any size lives in git
   history forever, is re-downloaded by every clone, and gains a new copy
   every time it is regenerated.

3. **Publish it under a tag that names the legacy version:**

   ```bash
   gh release create "snapshot-legacy-8.0.0.3-ci"   # first time only
   gh release upload "snapshot-legacy-8.0.0.3-ci" \
     snapshots/db.sql snapshots/sites.tar.gz snapshots/images.txt --clobber
   ```

   The version in the tag is the point: when the legacy system is upgraded
   you create a *new* tag rather than overwriting the old one, so an older
   branch still resolves the snapshot that matches its code.

4. **Pin that tag in exactly the places that consume it, and change them
   together.** Typically the developer-facing script and the pipeline
   definition. A mismatch between them is a confusing failure, so say
   explicitly, in whatever README covers the scripts, that both must change
   at once.

**Step 3 — Restore it in the pipeline.** The order matters and each step
has a reason:

1. **Download the snapshot by pinned tag.**
   ```bash
   gh release download "$SNAPSHOT_TAG" --dir snapshots \
     --pattern "db.sql" --pattern "sites.tar.gz" --pattern "images.txt"
   ```

2. **Start *only* the database**, and wait for its health check.
   ```bash
   docker compose up -d db --wait
   ```
   Only the database. Starting the legacy application now would trigger the
   very installer you are avoiding.

3. **Restore the dump.**
   ```bash
   docker compose cp snapshots/db.sql db:/tmp/db.sql
   docker compose exec -T db sh -c 'mariadb -uroot -p"$MYSQL_ROOT_PASSWORD" < /tmp/db.sql'
   ```

4. **Create the application's database user** — the dump does not have it
   (§21.4). Create it with this run's own generated password.

5. **Restore the file state into its volume.**
   ```bash
   docker volume create stack_sitesvolume
   docker run --rm -v stack_sitesvolume:/data -v "$PWD/snapshots:/backup" \
     alpine sh -c "tar xzf /backup/sites.tar.gz -C /data"
   ```

6. **Apply this run's credentials** (§21.4) — rewrite the application's
   config file, re-encrypt and store any secret that needs the
   application's own crypto, and set the database user's password.

7. **Now start everything else**, waiting on the orchestrator's own health
   checks.
   ```bash
   docker compose up -d --build --wait --wait-timeout 1200 legacy-app facade
   ```

The legacy application starts for the first time against an
already-installed volume, so it skips the installer entirely and is ready
in seconds.

**Step 4 — Give developers the same path.** Do not make the fast path
pipeline-only, or local setup stays slow *and* the restore path is only ever
exercised in the pipeline, where it is hardest to debug. Wrap the same
sequence in one script, with two modes: start or resume the existing stack,
and wipe the volumes and rebuild from the snapshot. Two details worth
copying: **download on demand**, so a new developer runs one command and
the script fetches the snapshot if it is not already there; and **warn
loudly about what the rebuild destroys**, since it deletes local database
and application volumes — the obvious mitigation being "take a snapshot
first," which is the same script from step 1.

**Step 5 — Shorten the timeouts you were forced to inflate.** This is the
step people forget, and where a lot of the real benefit is. Those generous
timeouts existed to accommodate the installer. Once the installer never
runs in your pipeline, revisit the health check's grace period (an
already-installed system needs a fraction of a fresh install's), the
pipeline's wait timeout (minutes, not tens of minutes), and any
provisioning script's retry budget (the tables it waits for now exist
before it starts). Lower them deliberately and watch for a run or two. **A
twenty-minute timeout on a one-minute operation is not safety, it is a
twenty-minute delay on every genuine failure.**

### 21.6 Gotchas and failure modes

| Symptom | Likely cause |
|---|---|
| Restore succeeds, application starts, but a feature fails decrypting its own stored data | You restored the database but not the installation's key material (§21.3). The archive of the application's writable directory is not optional |
| An invalid-client error on authentication after restore | A secret was written as plaintext instead of being encrypted with the restored installation's own keys (§21.4) |
| The application cannot connect to its own database after restore | Its config file still holds the snapshot's old password, or the database user was never created (§21.4) |
| Admin login fails with the password you expect | The snapshot contains the admin password *hash* from capture time; your configured admin password has to match the snapshot's |
| Works locally, fails in the pipeline (or the reverse) | A volume or network name derived from the project directory name, which differs between a local checkout and a runner. Read it from the running container rather than assuming it |
| The installer runs anyway, slowly | Something started the application before the restore finished. Start the database only, restore, *then* start the rest |
| A failed capture left a broken snapshot | The dump was not written to a temp file and moved into place |
| A failed capture left the stack stopped | No `trap` to restart the applications on exit |

### 21.7 Keeping it healthy over time

A snapshot is a frozen copy of something that keeps moving. Budget for:

- **Re-snapshot when the legacy version changes.** New version, new tag,
  update the pins. The old tag stays, so older branches keep working.
- **Re-snapshot when the schema changes** — including when *your own*
  provisioning adds structure the snapshot predates.
- **Watch for drift between the snapshot path and reality.** This is the
  real risk of the technique: your pipeline tests a restored installation
  forever, so a change that breaks the *installer* cannot be caught by it.
  **The mitigation is to keep one job that installs from scratch** — not on
  every commit, but nightly or weekly — so the from-scratch path is still
  exercised somewhere. §5's project did *not* do this: its pipeline only
  ever ran the restore path, so an installer-affecting regression would
  have gone unnoticed. Worth naming as a real gap rather than assuming the
  restore path's green runs cover it.
- **Document the coupling explicitly.** If restoring replaces the admin
  password hash, then the configured admin password must match the
  snapshot. Couplings like that are invisible and infuriating when
  undocumented.

### 21.8 When not to do this

The technique has real costs: an artifact to maintain, a publishing step, a
staleness risk, and credential re-application machinery. Skip it when:

- **The installer is already fast.** Under about a minute, the complexity
  is not worth it.
- **The installed state changes constantly.** If you would be
  re-snapshotting weekly, you are maintaining a second build artifact for
  small gain.
- **You are testing the installer or the upgrade path itself.** Then a
  from-scratch install is the thing under test — do not skip it.
- **A faster layer already solves it.** A pre-baked image containing the
  installed state, or your orchestrator's volume caching, can be simpler
  than a snapshot-and-restore pipeline, if the legacy system's licensing
  and your registry allow it. Note the trade-off: an image with the
  installed state baked in is simpler to restore but harder to inspect,
  sanitise, and prove contains no real data — which is exactly why
  separable, inspectable dump files are often worth the extra machinery.

## 22. Multi-service considerations

These apply once there is more than one backend service behind the same
login. Most legacy-auth migrations are heading here even if they have not
arrived, so the decisions below are worth making before the second service
exists rather than after.

- **One externally-reachable identity surface.** Every consumer — the
  frontend, and the facade's own login, callback, and session endpoints —
  shares one address, fronted by a gateway that makes no identity decisions
  of its own. Do not let each service grow its own login entry point.
- **Token validation belongs at the edge, once, not in every service.**
  Validating independently in N services is N places to get it wrong and N
  places to update when a signing key rotates. §5's migration named this
  explicitly as planned-but-unbuilt, with the facade validating only on its
  own identity endpoint — which is a reasonable interim position as long as
  it is stated rather than assumed solved.
- **The facade's own proxy should scale by adding routes to a gateway, not
  by learning every new service by name.** A browser session's calls go
  through one proxy destination pointed at the gateway; new services arrive
  behind that gateway rather than as new proxy destinations the facade must
  know individually. This is worth deciding ahead of the need, because it
  costs nothing now and saves a change to the facade per service later.
- **Service-to-service calls need their own answer — do not reuse the
  browser's session mechanism.** Defer machine-to-machine authentication to
  its own decision, against whatever is actually required when a real
  caller exists, rather than stretching the browser flow to cover it. **A
  session cookie is not a service credential**, and the way it becomes one
  is by default, not by decision.
- **Authorization is a separate, not-yet-answered layer.** The facade
  answers "who is this," not "what may they do with service X's data." Call
  this out as its own workstream per service, never an extension of the
  facade. This is the single most common place for a facade's scope to be
  silently over-claimed.
- **Each new service inherits the facade's accepted risks (§11) until it
  actually needs the gap closed.** Do not silently assume a gap is closed
  because time has passed; re-check it against the specific service's
  requirements. Multi-instance support and the absence of legacy-side
  revocation are the two that most often get inherited unexamined.
- **Scope widening is explicit, not incidental.** Request only the scopes a
  real caller actually needs. §5's migration deliberately did not include
  the API-access scopes the legacy system requires for proxied calls until
  a real caller needed them — widen per concrete need, never preemptively
  "to be safe for later."

---

# Part V — The gates

## 23. Migration checklist

This gates *building* the facade. Use it phase-gated against Part III — do
not treat it as a flat list to complete in order, since several items only
become relevant at a later phase.

**Phase 0 — before writing code**

- [ ] Answered every question in §6 with the actual stakeholder, in writing.
- [ ] Answered all eight discovery questions in §8 by observation, with
      evidence saved for each.
- [ ] Confirmed whether the legacy system exposes a standard protocol or
      only a proprietary login path.
- [ ] Decided: delegate credential verification, or build your own store?
      (Default: delegate.)
- [ ] Named any temporary stand-in explicitly, with its deletion criterion
      written down **before** it is built.
- [ ] Client registration is a re-runnable, idempotent script, not a
      hand-made record.

**Phase 1 — stand-in / unblock**

- [ ] Credentials, if they pass through the facade at all, are never logged
      and are cleared after single use.
- [ ] The never-logged guarantee is a test, run at the most verbose log
      level, with a companion assertion that log output existed at all.
- [ ] At least one integration test covers the real legacy system, not a
      mock of it, for the credential-verification path.
- [ ] Scopes and claims requested are minimal, not "everything, to be
      safe."
- [ ] No password policy is enforced at login.

**Phase 2 — real browser flow**

- [ ] Authorization-code, plus PKCE for public clients, replaces any
      password-in-the-client flow.
- [ ] Each of signature, issuer, audience, and nonce has its own test, with
      everything else valid and only that one thing wrong.
- [ ] The browser never receives the legacy system's own token in any
      response body, header, or cookie — proven by a test.
- [ ] Any post-login return-URL parameter is validated against a host
      allow-list, and its **rejection** path has a test.
- [ ] A session is established via cookie; HTTP-only, same-site, and secure
      flags are set (secure becomes genuinely true at Phase 5).

**Phase 3 — the session is real**

- [ ] The stateless-versus-server-side decision is written down, with its
      consequence stated.
- [ ] The cookie carries an opaque reference only; session contents live in
      a store the facade owns, separate from the legacy database.
- [ ] Every cryptographic key, certificate, and interim protocol state
      store survives a process restart — verified by restarting, not by
      reading configuration.
- [ ] Restart mid-login, after the redirect out and before the redirect
      back, still completes successfully.
- [ ] Revocation ends every session for that identity, no other identity's,
      requires authentication per endpoint, leaves nothing changed on an
      unauthorized attempt, and handles the self-targeting case.
- [ ] The legacy system's own token lifetime after revocation is documented
      in the ledger if it cannot be revoked server-to-server.
- [ ] Silent refresh works, with a stale-token fallback rather than an
      invented failure.
- [ ] Logout ends the session on **both** sides, using the legacy system's
      real logout contract verified against source or discovery.

**Phase 4 — the frontend reaches legacy data**

- [ ] A server-side proxy attaches the token; the frontend never holds one.
- [ ] Legacy error statuses relay through intact.
- [ ] Scope widening, if needed, happened as its own deliberate change.
- [ ] Request bodies through the proxy are proven not to reach any log, by
      the same test shape as Phase 1.

**Phase 5 — one origin and TLS**

- [ ] One public address serves both the frontend and the facade's
      endpoints; the gateway makes no identity decisions.
- [ ] The registered redirect URI points at the gateway, and the legacy
      system itself confirms it accepts it.
- [ ] TLS terminated at the gateway; plain HTTP redirected; HSTS sent.
- [ ] Forwarded headers trusted only from a fixed, configured network, with
      tests from inside and outside it, and a startup failure on invalid
      configuration.

**Phase 6 — delete the stand-in**

- [ ] Confirmed, with evidence, that nothing but its own tests calls the
      stand-in path.
- [ ] Deleted its code, its authentication scheme if now unused, its tests,
      and any now-empty layers or modules — not left as dead scaffolding.
- [ ] Regenerated any committed API snapshot or client that referenced it.
- [ ] Any coverage the removal cost is either replaced or recorded in the
      ledger.

**Phase 7 — harden**

- [ ] Rate limiting on every unauthenticated entry point, one shared budget
      per flow, with boundary, shared-budget, and not-applied-elsewhere
      tests.
- [ ] A long-lived circuit breaker per outbound dependency, counting only
      real failures, with both the opens-on-errors and
      never-opens-on-rejections tests.
- [ ] Audit log covering login outcomes, proxied access, and admin actions,
      in one stably named category, with zero credentials, tokens, or
      bodies.
- [ ] What is deliberately *not* audited is written down, with why.
- [ ] Startup ordering explicit: the app's own readiness poll fails open,
      provisioning fails closed, and the orchestrator enforces both
      dependencies.
- [ ] The whole stack comes up from nothing, in one command, producing a
      working login with no manual step.
- [ ] Every security-relevant setting has a test proving invalid values
      refuse to start the host.

**Phase 8 — scale**

- [ ] Audited every per-process artifact — temporary files, in-memory key
      rings, ephemeral certificates, interim protocol state.
- [ ] Shared backing store and shared key ring in place, or session
      affinity is an explicit, written-down interim measure.
- [ ] Two instances, no affinity: login completes across instances,
      authenticated requests succeed on either, revocation on one is
      honoured by the other.
- [ ] A rolling deploy has been tested both mid-login and mid-session, not
      just a clean restart.

**Phase 9 — write the capstone**

- [ ] One document states the finished design's guarantees end to end.
- [ ] One section lists accepted risks explicitly, each with why it is
      accepted and what would change that.
- [ ] Downstream teams and services know they inherit those risks until
      they personally need the gap closed.

**Ongoing — every new service added behind the facade**

- [ ] Token validation happens at the edge or gateway, not reinvented per
      service.
- [ ] Service-to-service authentication has its own decision, not a reuse
      of the browser session mechanism.
- [ ] Authorization — what, not just who — is designed per service; it is
      not inherited from the facade.
- [ ] The service's own inheritance of the risk ledger has been reviewed
      rather than assumed stale.

## 24. Production readiness checklist

§23 gates *building* the facade. This gates **exposing it to real users and
real data**, which is a separate and higher bar. Treat every unchecked item
as an explicit accepted risk (§11), not a silent gap, until it is checked.

It is worth being blunt about what this distinction means in practice. A
facade can satisfy every item in §23 — a real browser flow, revocable
sessions, rate limits, circuit breakers, an audit trail, two instances
proven — and still be nowhere near production-ready, because production
adds real secrets, real network exposure, real data, and real consequences.
§5's migration reached exactly that point: structurally complete, reviewed
end to end, and explicitly **not** approved for production use, with the
items below as the stated reasons rather than as oversights. That is a
respectable place to be, as long as it is said out loud.

**Secrets and key management**

- [ ] Every secret — database passwords, the OAuth client secret,
      certificate passphrases — comes from a real secrets manager with
      access control and rotation, not a config file with known and shared
      credentials.
- [ ] Encryption and signing certificates are CA-issued or otherwise
      trust-chain-valid, not self-signed development certificates from a
      local setup script, and have a documented rotation procedure with
      no-downtime rollover.
- [ ] Any session or state key ring has a rotation plan that does not
      invalidate every live session at once.
- [ ] Per-environment separation is real: distinct identity-provider
      configuration, database and storage credentials, encryption and
      signing keys, and observability destinations, with no lower-trust
      environment able to reach a higher-trust one.

**Scaling and availability**

- [ ] Multi-instance operation is *proven*, not merely unblocked. Closing
      the structural blockers — shared key ring, persisted certificates,
      shared interim state store — is a precondition, not the same claim as
      having run two replicas under load and through a rolling deploy.
- [ ] A rolling deploy mid-login and mid-session has been tested, not just
      a clean restart.
- [ ] Resource limits (CPU and memory) and autoscaling or capacity targets
      are set from an actual load test, not left at orchestrator defaults
      (§20.6).
- [ ] Rate-limiter keying has been re-validated against the real production
      network path. A limiter keyed on remote address behaves differently
      once there is more than one hop or load balancer in front of it than
      it does behind a single local gateway.

**Resilience and dependency on the legacy system**

- [ ] Every known upstream legacy-system defect affecting authentication —
      the server-error-on-wrong-password case in §8, question 8 being the
      canonical example — is either fixed upstream, has a compensating
      control, or is a formally signed-off accepted risk. Not silently
      unguarded.
- [ ] Circuit breakers, rate limits, and timeouts are tuned from real
      traffic and latency data, not the conservative defaults chosen for a
      local stack with no real load.
- [ ] A degraded-legacy-system runbook exists: what a human does when the
      circuit breaker trips, who gets paged, what users see.

**Authorization**

- [ ] A real authorization model exists for what an authenticated identity
      may do with legacy and new-service data. "The facade confirms
      identity only" is an accepted risk at prototype stage, not a
      production posture.
- [ ] Admin-level actions — session revocation, impersonation, support
      tooling — are gated by a real role or claim, not a fixed config-file
      allow-list.
- [ ] Scopes requested are exactly what is needed for what is actually
      built; widened deliberately per real caller, never preemptively.

**Observability and incident response**

- [ ] Metrics and distributed tracing exist across the facade and the
      legacy-system boundary, not just the audit log (§18.3).
- [ ] A readiness endpoint exists that actually checks the facade's own
      critical dependencies, distinct from a liveness check (§18.2).
- [ ] Audit log coverage, retention, and redaction match your actual
      compliance regime — logs redact tokens, credentials, personal data,
      and sensitive content by default — not just "login outcomes and the
      proxy."
- [ ] Alerting exists for the security-relevant signals that matter in
      production: spikes in failed logins, circuit-breaker trips,
      rate-limit rejections, and session-revocation calls.
- [ ] An incident runbook exists for a suspected compromised session or
      credential: who can revoke it, how fast, and what gets checked
      afterwards.
- [ ] Backup and restore is tested for whatever now holds session and
      identity state, including a timed restore-from-backup drill — not
      just a volume that happens to persist.

**Supply chain and build**

- [ ] Every base and runtime image is pinned by digest, with a documented,
      deliberate exception policy where it is not (§20.1) — not silently
      drifting to "pin everything except what is inconvenient."
- [ ] Dependency and container image scanning runs in the pipeline, not
      just at build time (§20.6).
- [ ] Schema management has moved from automatic creation at startup
      (acceptable while the schema is small and moving) to versioned,
      reviewed migrations with a tested rollback path (§15.8).
- [ ] If a captured fixture of the legacy system's installed state is used,
      a periodic from-scratch install still runs somewhere, so an
      installer-affecting regression cannot hide (§21.7).

**Compliance and sign-off**

- [ ] Whatever regulatory framework governs the data this system will touch
      has been reviewed against the actual design — **not assumed satisfied
      because credentials are delegated to a legacy system that was itself
      compliant.** Delegating credential verification does not delegate
      your own obligations.
- [ ] A security review or penetration test has been performed against the
      **production** configuration specifically. A review of the local or
      development stack is necessary but not sufficient: production adds
      real network exposure, real secrets, and real data.
- [ ] The vulnerability-disclosure and triage process is live and staffed
      before production traffic exists, not written and left dormant.
- [ ] A rollback plan exists: if the facade must be pulled from production,
      what happens to active sessions, in-flight logins, and the legacy
      system's own client registration.

## 25. Putting this into practice

1. Start with §6 and §7, and get real answers from real stakeholders before
   designing anything. Then do §8's discovery by observation.
2. Walk the decision graphs in §9 to settle the big architectural forks
   early. Revisit §9.4 specifically at whatever milestone first makes a
   dedicated identity service plausible — and not before.
3. Build in Part III's phase order: learn → stand-in → real flow → real
   session → proxy → one origin → delete the stand-in → harden → prove
   scale. Do not skip ahead to hardening before the real flow exists, and
   do not skip deleting the stand-in once it is unused.
4. Use Part IV as working checklists *while* building, not as a
   retrospective audit. §14 while designing, §15 while wiring,
   §16 and §17 while testing, §18 while instrumenting, §19 and §20 while
   automating, §21 when the pipeline gets slow, §22 before the second
   service.
5. Keep the risk ledger (§11) alive continuously, and gate production
   exposure on §24 — never on §23 alone.

---

# Appendix A — One stack's concrete instantiation

The body of this handbook is deliberately stack-agnostic. This appendix is
the opposite on purpose: the actual libraries and commands behind the
facade described in §5, which happens to be .NET. It is here as a concrete
starting point if yours is too, and as a translation key if it is not —
each row names the *purpose*, which is the part that transfers.

### A.1 Libraries, by purpose

| Purpose | Package(s) | Notes |
|---|---|---|
| OIDC/OAuth **client**, browser flow | `OpenIddict.Client`, `OpenIddict.Client.AspNetCore`, `OpenIddict.Client.SystemNetHttp` | Drives authorization-code + PKCE against the legacy system. Note this is the *client* half of the library; the server half would make you an identity provider (Phase 2.1) |
| The protocol library's own interim state persistence | `OpenIddict.EntityFrameworkCore` | Backs the state-token store that otherwise defaults to a per-process temporary file — the single-instance trap in §7 and Phase 3.2 |
| Session cookie ticket protection | `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` | Puts the key ring protecting session cookies in the shared store, so a restart does not log everyone out (Phase 3.3) |
| Session/state database access | `Microsoft.EntityFrameworkCore.Sqlite` (local fallback), a MySQL/MariaDB provider, `MySqlConnector` | The configuration-selected fallback-versus-real-backend fork from §15.1 — and the reason §16.3 insists on testing both branches |
| Certificate loading for persisted certificates | `System.Security.Cryptography.Pkcs` | Loads the PKCS#12 files that replace per-boot generated certificates (§15.2) |
| Resilience / circuit breakers | `Polly` (`Polly.Core`), `Microsoft.Extensions.Http.Polly` | The shared long-lived pipeline per dependency from §15.5 — one instance, not one per call |
| Reverse proxy | `Yarp.ReverseProxy` | Used twice, for two different jobs: the gateway that provides one public origin (Phase 5.1), and the facade's own server-side BFF proxy (Phase 4.1) |
| JWT parsing/validation in tests | `System.IdentityModel.Tokens.Jwt` | Used by the protocol-speaking fake identity provider (§16.2) and wherever identity-token claims are read |
| API document + interactive docs UI | `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore` | Development-only, gated behind an explicit development check so a forgotten environment variable fails toward less exposure (§15.1) |
| Typed client generation from a committed snapshot | NSwag, invoked as a script rather than a package reference | Generates the frontend's typed client from a committed API document; the pipeline regenerates and diffs it (§19.3). Remember the generator needs a *running* instance, not just source |
| Options binding and validation | `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.Extensions.Options.DataAnnotations` | The validate-on-start pattern from §15.1 — applied per option class, deliberately not to the admin allow-list |
| UI components | `MudBlazor` | The frontend and login UI |
| Testing framework | `xunit` | All three test layers from §16.1 |
| Coverage collection | `coverlet.collector` | Referenced in every test project, so A.3's coverage command needs no extra install |

### A.2 Tools worth knowing about

- **`global.json`** pins the SDK version, and is the single source of truth
  both local development and the pipeline read — the one-file toolchain
  pinning from §19.5. Run `dotnet --version` in the repository root to
  confirm the resolved SDK matches what you expect; a mismatch here is a
  common source of "works on my machine."
- **Scalar** is the interactive API document UI, reachable only in the
  development environment. A lighter-weight alternative to Swagger UI, used
  purely as a developer convenience and never shipped to a real
  environment.
- **NSwag** generates the typed client from the committed API document. It
  is invoked as a script against a live throwaway instance, then diffed
  against the commit in the pipeline. If you adopt this pattern, remember
  the generator needs a running instance to regenerate against — which is
  why §19.3 insists the regeneration step runs *after* the stack is up.
- **Architecture-test libraries** (ArchUnitNET, NetArchTest) express
  "layer A must never reference layer B" fluently at scale. §5's project
  used hand-rolled reflection over the assembly's referenced-assemblies
  list instead, which works fine at a few layers and a handful of rules.
  Reach for a library when the rules outgrow that.
- **There is no project-local tool manifest** in §5's project, so no CLI
  tools are pinned per-repository. Any tool below that is not part of the
  SDK needs either a global install or a manifest added deliberately; do
  not assume it is present on a clean checkout.

### A.3 Helpful `dotnet` commands

**Build and restore**

```bash
# Restore and build the whole solution, matching the pipeline's own steps.
dotnet restore MySolution.slnx
dotnet build MySolution.slnx --configuration Release --no-restore
```

**Running tests**

```bash
# Every test project in the solution.
dotnet test MySolution.slnx --configuration Release --no-build --no-restore

# Just the fast, in-process layers (unit + architecture), skipping anything
# that needs the live stack up (§16.1).
dotnet test tests/.../UnitTests
dotnet test tests/.../ArchitectureTests

# The black-box integration layer — requires the stack to actually be
# running first.
dotnet test tests/.../IntegrationTests --filter Category=Integration

# One test by name, while iterating on a single security case (§17).
dotnet test --filter "FullyQualifiedName~Revoking_a_session_signs_it_out_immediately"
```

**Coverage**

```bash
# Collect coverage via the collector already referenced in every test
# project — no extra install needed for this step alone.
dotnet test MySolution.slnx --collect:"XPlat Code Coverage" --results-directory TestResults

# Turn the resulting Cobertura XML into a browsable HTML report. Requires a
# one-time global tool install.
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:"TestResults/coverage-report" -reporttypes:Html
```

**Security and dependency scanning**

```bash
# Known-vulnerable packages, including transitive ones — the dependency
# half of §24's scanning requirement (§20.6 covers the image half).
dotnet list package --vulnerable --include-transitive

# Deprecated packages, often a precursor to an unpatched vulnerability.
dotnet list package --deprecated

# Outdated packages generally — not a security signal by itself, but the
# list to review before deciding a vulnerable package's fix is "upgrade."
dotnet list package --outdated

# A restore-time audit also runs by default on recent SDKs and prints
# warnings for known vulnerabilities as part of an ordinary restore. Worth
# not silencing those warnings in the pipeline, even if noisy at first.
dotnet restore MySolution.slnx
```

**Formatting and static analysis**

```bash
# Check formatting without modifying files (good for a pipeline gate);
# drop --verify-no-changes to apply fixes locally.
dotnet format MySolution.slnx --verify-no-changes

# Surface analyzer warnings as errors temporarily, to see everything at
# once without permanently flipping the setting in the repository.
dotnet build MySolution.slnx -warnaserror
```

**Schema, once §15.8 moves past automatic creation**

```bash
# Requires the EF Core tools (dotnet tool install -g dotnet-ef) and a
# DbContext-aware startup project.
dotnet ef migrations add <Name> --project src/.../Api
dotnet ef database update --project src/.../Api
```

**Other**

```bash
# Confirm which SDK global.json resolves to (A.2).
dotnet --version

# Regenerate and verify the committed API client snapshot — the same check
# the pipeline runs (§19.3), runnable locally before pushing.
bash scripts/dev/generate-openapi.sh
git diff --exit-code -- src/.../OpenAPIs/Api.json

# Run one service directly, outside containers, for fast local iteration.
# Picks up development settings and the configuration-selected local
# fallbacks from §15.1.
dotnet run --project src/.../Api
```
