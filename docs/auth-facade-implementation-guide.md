# Implementation guide: building an authentication facade, step by step

A sequential, developer-facing build order for putting a modern login in
front of a legacy system that owns the user accounts. This is the "do this,
then that" companion to the framework documents in this directory: the
framework tells you *what the decisions are and why*; this guide tells you
*what to build next*.

No code, no specific language or vendor. Every step is phrased as work you
can do in any stack, with a verifiable finish line.

## How to use this guide

Each step follows the same shape:

- **Goal** — one sentence on what this step produces.
- **Why now** — why this step comes here and not earlier or later.
- **Do this** — the actual work, in order.
- **Done when** — observable, verifiable exit criteria. If you can't
  demonstrate these, you are not done, regardless of how much code exists.
- **Common mistakes** — what goes wrong here specifically.
- **Don't do yet** — work that belongs to a later step. This is as
  important as the "do" list: most trouble in this kind of project comes
  from doing step 7's work during step 2.

**Follow the phases in order.** Within a phase, individual steps are
usually sequential too, but where two steps can be worked in parallel it
says so.

**The single most important discipline:** finish a step's "Done when"
before starting the next one. Each phase assumes the previous one actually
works, and debugging a half-finished login flow underneath a half-finished
session layer is dramatically harder than debugging either alone.

## Build order at a glance

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

Rough expectation: phases 0–2 are where you learn most of what you didn't
know; phases 3 and 7 are the bulk of the work; phase 6 takes an afternoon
and is the one teams skip.

---

## Phase 0 — Learn the legacy system

No code in this phase. Everything you build later depends on facts you do
not have yet, and guessing them is the single most common source of rework.

### 0.1 Get a local, disposable instance of the legacy system running

**Goal:** a legacy system you can break, reset, and inspect freely.

**Why now:** you cannot discover what it supports (0.2) by reading its
documentation alone — vendor docs and actual behavior routinely disagree,
and the actual behavior is what you have to build against.

**Do this:**
1. Stand up the legacy system locally, in a container or VM, with
   synthetic data only.
2. Make it reproducible from scratch — a script or compose file, not a
   hand-assembled machine you'll be afraid to delete.
3. Confirm you can reset it to a known state and that a fresh start
   produces a working login through its own UI.

**Done when:** you can destroy the whole thing and have it back, working,
with one command — and you've done it at least once.

**Common mistakes:**
- Sharing one instance across the team. You will each need to break it.
- Using a copy of production data. Use synthetic or properly sanitized
  data; nothing in this phase needs real records.

**Don't do yet:** any integration. Just get it running.

### 0.2 Find out what it actually supports

**Goal:** a written list of the legacy system's authentication
capabilities, verified by observation rather than documentation.

**Why now:** this list determines which branch of every decision graph in
the framework applies to you.

**Do this** — for each item, verify by actually calling it, not by reading
about it:
1. **Does it speak a standard protocol?** Look for a discovery document
   (commonly at a well-known path). Save a copy. If there's no discovery
   document, find out whether there's an OAuth/OIDC implementation at all.
2. **Which grant types does it support?** Note which are enabled by
   default vs. require configuration.
3. **Where are its signing keys published?** (A JWKS endpoint, usually.)
   Confirm you can fetch them.
4. **Does it have a logout endpoint**, and what exactly does it require?
   (Parameter names, whether a redirect URI must match exactly, whether it
   needs a token hint.) Read its source if you have it; logout contracts
   are frequently undocumented.
5. **Does it support server-to-server token revocation?** Most legacy
   systems do not. If it doesn't, write that down now — it becomes a
   permanent, structural limitation of everything you build (see 3.4).
6. **How does it register client applications?** A UI, a database table, a
   config file, an API? Who owns that registration going forward?
7. **What are its real credential rules?** Not the policy for new
   accounts — the rules for accounts that already exist, which may predate
   any policy.
8. **How does it behave on a wrong password?** Specifically: does it
   return a clean rejection, or does it ever return a server error? Note
   any defects; you'll need to decide whether to compensate for them.

**Done when:** you have a short written document answering all eight,
each with evidence (a saved response, a source file reference, a
reproduction).

**Common mistakes:**
- Trusting the discovery document's advertised capabilities without
  calling them. Advertised ≠ implemented.
- Not checking logout and revocation, because they feel like later
  problems. They constrain your session design, which is Phase 3.

**Don't do yet:** choosing libraries or writing an architecture document.

### 0.3 Register a client application, reproducibly

**Goal:** the legacy system has a registered client for your facade, and
the registration is created by a script you can re-run.

**Why now:** every later phase authenticates as this client. A
hand-registered client that nobody can recreate becomes a blocker the
first time someone else sets up the project.

**Do this:**
1. Register a client with the minimum scopes needed to identify a user.
   Resist adding more "for later."
2. Make the registration a script (idempotent — safe to run repeatedly;
   it should create if missing and update if present).
3. Generate the client secret; never type a memorable one, and never
   commit it.
4. Note exactly which fields your script sets, and which it doesn't. If
   it can't update some field after creation, write that down — it's a
   trap for whoever changes that field later.

**Done when:** running the script twice in a row succeeds both times and
leaves the same correct result, and a teammate can set up from scratch
using it.

**Common mistakes:**
- Writing the registration directly into a database without matching how
  the legacy system itself writes it (encryption, hashing, defaults).
  Check how its own code creates a client, and match it.
- Over-scoping the client because it's easier than coming back later.

### 0.4 Decide and write down your stand-in's removal criterion

**Goal:** one sentence in your repository stating what has to be true
before you delete the Phase 1 stand-in.

**Why now:** this is the only moment this will ever be easy to write.
Afterward, the stand-in works, and nobody wants to touch it.

**Do this:** write, in a decision record or the README, something of this
shape: *"The [stand-in] exists only to unblock [X]. It will be deleted
when [the real browser flow works end to end through the gateway]. It is
not to be used by any new client."*

**Done when:** it's committed, and the team has seen it.

---

## Phase 1 — Verify a credential, end to end

**The point of this phase is learning, not architecture.** You are proving
you can make the legacy system answer "is this person who they claim to
be," and finding out what it does when things go wrong. Everything you
build here is scaffolding you will delete in Phase 6.

### 1.1 Build the smallest possible credential check

**Goal:** one endpoint on your facade that takes a username and password,
asks the legacy system, and returns success or failure.

**Why now:** it's the shortest path to a working round trip, which is what
teaches you the legacy system's real behavior.

**Do this:**
1. Add one endpoint that accepts credentials.
2. Call whatever the legacy system offers for direct credential
   verification (commonly a password/credential grant on its token
   endpoint).
3. Return a minimal success/failure result. Don't design a response
   format; it's temporary.
4. Add bounds on input length, matched to what the legacy system can
   actually store — as a guard against absurd input, not as a password
   policy. Your facade must never reject a credential the legacy system
   would have accepted.

**Done when:** a correct credential returns success and an incorrect one
returns a clean failure, both verified by hand against the real local
legacy system.

**Common mistakes:**
- **Enforcing a password policy at login.** Existing accounts may violate
  any policy you invent, and you'll lock out real users. Policy belongs
  where accounts are created, which isn't here.
- Designing a token/response format. It's getting deleted.

**Don't do yet:** sessions, cookies, tokens for clients, redirects.

### 1.2 Prove credentials never reach a log

**Goal:** confidence, backed by a test, that no credential appears in any
log output.

**Why now:** this is the phase where credentials actually pass through
your process. It's also the easiest moment to build the test that keeps
being true later.

**Do this:**
1. Audit your own logging calls for anything that includes the request
   body or credential fields.
2. Write a test that drives a login with a recognizable marker value as
   the password, with logging turned up to its most verbose level, and
   asserts the marker appears nowhere in captured output.
3. Make that test also assert that *some* log output was captured — so if
   your capture mechanism silently breaks later, the test fails instead
   of passing for the wrong reason.
4. If you must hold the plaintext at all, retrieve it once into a local
   variable, use it, and clear it; don't pass it around.

**Done when:** the test exists, passes, and fails if you deliberately add
a logging statement that includes the password.

**Common mistakes:**
- Testing at the default log level only. The risk lives at verbose
  levels, and a misconfigured deployment can reach them.
- Assuming your own code is the only thing that logs. Frameworks and
  HTTP libraries log too.

### 1.3 Add one integration test against the real legacy system

**Goal:** an automated test that fails if the legacy system stops
accepting your client or your credential check.

**Why now:** you're about to build a lot on top of the assumption that
this works. Make it a test, not a memory of a manual check.

**Do this:**
1. Write a test that runs against the real, running local legacy system
   (not a fake).
2. Have it verify a correct credential succeeds, and an incorrect one
   fails in the specific way you observed in 0.2.8.
3. Mark/categorize it so it can be run separately from fast unit tests —
   it needs the stack up.

**Done when:** the test passes with the stack running, and is clearly
separated from tests that don't need the stack.

**Don't do yet:** a readiness fixture or elaborate test infrastructure.
One test, run manually, is enough at this point.

---

## Phase 2 — The real browser login flow

This is the login you're actually shipping. The user will type their
password into the legacy system's own login page, and your facade will
never see it.

### 2.1 Choose a protocol library — do not hand-roll

**Goal:** a library in your project that implements the
authorization-code flow (with PKCE) as a client.

**Why now:** before you write any flow code, because the library's shape
will influence how you structure the endpoints.

**Do this:**
1. Pick a well-maintained OAuth2/OIDC *client* library for your stack
   (you are the client here; the legacy system is the server).
2. Verify it supports: PKCE, validating the identity token's signature
   against a published key set, nonce checking, and refresh.
3. Confirm how it stores the short-lived state it needs between the
   redirect out and the redirect back. **Note where that lives** — if
   it's an in-process file or memory, you have a single-instance
   constraint to resolve in Phase 8.

**Done when:** the library is added, and you've written down where it
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
1. Add an endpoint (e.g. a `/login`-shaped route) that triggers the
   library's redirect to the legacy system's authorize endpoint.
2. Confirm the outgoing redirect contains the expected parameters,
   including the PKCE challenge, a state value, and a nonce.
3. Accept an optional "where to go after login" parameter — and
   **validate it against an allow-list of hosts you control**, falling
   back to a safe default when it doesn't match. An unvalidated
   post-login redirect is an open-redirect vulnerability.
4. Write a test that drives the rejection path of that validation
   (a foreign host, a non-HTTP scheme, a malformed value), not just the
   happy path.

**Done when:** hitting the endpoint in a browser lands you on the legacy
system's real login page, and the redirect validation's rejection path is
covered by a test.

**Common mistakes:**
- Forgetting the allow-list on the return URL, or writing it but only
  testing that valid values pass.

### 2.3 Add the callback endpoint and validate properly

**Goal:** an endpoint the legacy system redirects back to, which
exchanges the code for tokens and verifies the identity token.

**Do this:**
1. Add the callback route, registered to exactly the URI the client
   registration in 0.3 declares. It must match byte for byte — check
   whether the legacy system compares exactly or by prefix (0.2.4).
2. Let the library redeem the code server-to-server.
3. Ensure it validates, at minimum: the token's signature against the
   legacy system's published keys, the issuer, the audience, and the
   nonce you sent in 2.2.
4. On any failure, redirect to a generic error — **never surface the
   legacy system's own error text** to the browser.
5. Write one test per validation check, each with *everything else valid
   and only that one thing wrong*. A token that's invalid in every
   dimension proves nothing about which check caught it.

**Done when:** a real browser login completes; and you have a test for
each of signature, issuer, audience, and nonce failing independently.

**Common mistakes:**
- Assuming the library validates something it doesn't. Verify each check
  with a test rather than trusting defaults.
- Returning useful error detail to the browser. It helps attackers more
  than users.

### 2.4 Establish the session

**Goal:** a successful callback leaves the browser holding a session
cookie, and nothing else.

**Do this:**
1. Sign the user into a cookie-based session on callback success.
2. Set the cookie flags correctly: HTTP-only, same-site restricted, and
   secure. (Secure won't actually be true until Phase 5 adds TLS — write
   the code expecting it now.)
3. Add one protected endpoint that returns the current user's identity,
   so you can confirm the session works.
4. Keep the tokens the exchange produced — you'll need them in Phase 4.
   Store them inside the session, not anywhere the browser can reach.

**Done when:** after a browser login, the protected endpoint returns your
identity, and the browser's cookie store contains only an authentication
cookie — no access or refresh token anywhere in the browser.

**Common mistakes:**
- Returning tokens in the callback response "so the frontend can use
  them." This is the single decision that most undermines the whole
  design. The frontend doesn't need them; Phase 4 solves that properly.
- Having two authentication mechanisms live at once (the Phase 1
  stand-in's and this one) without being clear which one protected
  endpoints actually require. Check explicitly.

### 2.5 Verify the browser never receives a token

**Goal:** a test that fails if a token ever appears in a browser-visible
response.

**Do this:** write a test that completes a login and inspects every
response body, header, and cookie for the token values the legacy system
issued. Assert they appear nowhere.

**Done when:** the test passes, and fails if you deliberately add the
token to a response.

---

## Phase 3 — Make the session real

Right now you have a session that works until something restarts, can't be
revoked, and expires awkwardly. This phase fixes all three. It's the
largest phase; do the steps in order, since each builds on the last.

### 3.1 Decide: stateless cookie or server-side session record

**Goal:** a written decision, with its consequence stated.

**Why now:** everything else in this phase depends on the answer, and
changing it later means changing all of it.

**Do this:** answer one question — *must a session be revocable before it
naturally expires?* If yes (or if you will ever run more than one
instance), you need a server-side session record. If genuinely no, a
self-contained encrypted cookie is acceptable, **and you must write down
that revocation does not exist** so nobody later assumes it does.

**Done when:** the decision and its consequence are committed somewhere
durable.

### 3.2 Move the session's contents server-side

**Goal:** the cookie carries only an opaque key; the real session
contents live in a store you control.

**Do this:**
1. Give the facade its own database/store — separate from the legacy
   system's, with its own least-privilege credentials. It holds only
   session state; it is not a second copy of user data.
2. Move the session contents (identity claims, and the legacy tokens from
   2.4) into one record per session.
3. Replace the cookie's payload with a reference to that record.
4. Point your protocol library's interim state storage (2.1) at the same
   store, replacing any per-process file.

**Done when:** you can look at a session cookie and see it's meaningless
on its own; deleting the corresponding record makes that cookie stop
working immediately.

**Common mistakes:**
- Putting session state in the legacy system's own database. Keep the
  boundary clean: the legacy system's data is not yours to grow.
- Leaving the protocol library's interim state in a local file. That's
  one of the two things that breaks under multiple instances (Phase 8).

### 3.3 Make key material and certificates survive a restart

**Goal:** restarting the facade does not log everyone out.

**Why now:** before you build revocation and refresh on top, since both
assume sessions survive normal operations.

**Do this:**
1. Find every cryptographic key or certificate your facade generates at
   startup — typically the key ring protecting session cookies, and any
   certificates your protocol library uses for its own interim state.
2. Replace each with a persisted one: a file in durable storage, or rows
   in the store from 3.2, with a documented generation step for each.
3. Verify by experiment, not inspection.

**Done when:** you log in, restart the facade, reload the page, and are
**still logged in**. Then: start a login, restart the facade mid-flow
(after the redirect out, before the redirect back), complete it, and
confirm it still succeeds.

**Common mistakes:**
- Testing only the "already logged in" case, and missing that the
  mid-login interim state has its own, separately-generated protection.
- Declaring this done from reading configuration. Restart the process and
  observe.

### 3.4 Add revocation

**Goal:** a session can be ended server-side without the cooperation of
whoever holds the cookie.

**Do this:**
1. Add a self-service endpoint that ends **all** sessions for the calling
   user's identity — not just the caller's own session. The threat model
   is "someone else has a copy of my session," and that copy is a
   different record.
2. Test it with two independent browser sessions for the same user:
   revoking from one must end both.
3. Test that it does **not** affect a different user's sessions.
4. Then add an admin-initiated version that ends another user's sessions.
   Gate it on something — even a configured allow-list is acceptable as a
   starting point, provided it defaults to empty (nobody) and you write
   down that it isn't a real authorization model.
5. Test the unauthorized path: a non-admin attempt must be refused
   **and** leave the target's sessions alive. Assert both.
6. Remember the limitation from 0.2.5: if the legacy system has no
   server-to-server revocation, its own token remains valid until it
   expires. Your revocation covers your sessions, not the legacy
   system's. Write this in the risk ledger.

**Done when:** all four tests pass (revoke reaches every session of that
user; reaches no other user's; requires authentication; unauthorized
attempts change nothing).

### 3.5 Add silent token refresh

**Goal:** an expired legacy access token renews itself without the user
noticing.

**Do this:**
1. Before using a stored access token, check whether it's expired or
   close to it; if so, redeem the refresh token and update the session
   record.
2. On refresh failure, fall back to the stale token and let the legacy
   system's own rejection decide the outcome — don't invent your own
   failure.
3. Test with a deliberately already-expired token so you don't have to
   wait out a real expiry.

**Done when:** a request made with an expired stored token succeeds, and
the session record afterward holds a new token.

### 3.6 Add logout, on both sides

**Goal:** logging out ends the session in your facade *and* in the legacy
system.

**Do this:**
1. End the local session (delete the record from 3.2).
2. If the legacy system supports a logout endpoint (0.2.4), redirect
   there afterward with exactly the parameters it requires.
3. Confirm the post-logout redirect target is registered exactly as the
   legacy system expects to compare it.

**Done when:** after logout, the protected endpoint rejects you, and
starting a new login prompts for credentials again rather than silently
signing you straight back in (which is the symptom of the legacy
system's own session still being alive).

**Common mistakes:**
- Only ending your own session. The user is still logged into the legacy
  system, so the next login appears to skip authentication entirely —
  alarming, and a real risk on shared machines.

---

## Phase 4 — Let the frontend reach legacy data

Your frontend now has a session but no way to call the legacy system's
API — because, correctly, it has no token.

### 4.1 Add a server-side proxy

**Goal:** the frontend calls your facade; your facade forwards to the
legacy API with the session's token attached server-side.

**Do this:**
1. Add a route prefix on the facade that forwards matching requests to
   the legacy system's API.
2. On each forwarded request, look up the session's access token (via
   3.5, so it's fresh) and attach it to the outbound request.
3. Relay the legacy system's response back unchanged — including its
   error statuses. Don't reshape or swallow them.
4. Confirm the scopes your client requested (0.3) actually cover the API
   calls you're forwarding. If not, widen them deliberately, now, as its
   own change.

**Done when:** the frontend successfully calls a legacy API endpoint
having never held a token, and a legacy error status arrives at the
frontend intact.

**Common mistakes:**
- Forwarding the browser's cookie to the legacy system instead of the
  token. Different systems, different credentials.
- Discovering mid-phase that your client's scopes were too narrow and
  quietly widening them everywhere. Make scope changes explicit.

### 4.2 Decide what the proxy forwards to

**Goal:** a decision, recorded, about how this proxy grows.

**Do this:** decide now whether the proxy forwards to each backend
service by name, or to a single gateway that routes onward. Prefer the
gateway: it means adding a service later doesn't require changing the
facade at all.

**Done when:** the decision is written down. You don't need the gateway
to exist yet.

### 4.3 Prove request bodies never reach a log

**Goal:** the same guarantee as 1.2, now for the data flowing through the
proxy.

**Why now:** this is the path real user data will travel. It's a
different code path from Phase 1's, so it needs its own test.

**Do this:** repeat 1.2's test shape — plant a marker in a proxied request
body, logging at maximum verbosity, assert the marker appears in no log
entry, and assert some log output existed to search.

**Done when:** the test passes and fails when you deliberately log the
body.

---

## Phase 5 — One public origin and TLS

### 5.1 Put a gateway in front

**Goal:** the browser talks to exactly one address for both the frontend
and the facade's own endpoints.

**Why now:** sessions are origin-scoped. Serving your frontend and your
login from different origins causes cookie problems that are confusing to
diagnose and tempting to "fix" by loosening cookie restrictions.

**Do this:**
1. Add a reverse proxy/gateway in front of both, routing by path.
2. Give it no identity logic of its own — it routes, it doesn't decide.
3. Update the client registration (0.3) so the registered redirect URI is
   the **gateway's** address, not the facade's own.

**Done when:** a complete login works through the gateway's address only,
and the facade's own address is no longer needed by a browser.

**Common mistakes:**
- Forgetting to update the registered redirect URI, producing an
  invalid-client error that looks like a credential problem.

### 5.2 Terminate TLS and trust forwarded headers carefully

**Goal:** the whole flow is HTTPS, and the facade knows the real client
address and protocol.

**Do this:**
1. Terminate TLS at the gateway. Redirect plain HTTP; send HSTS.
2. Have the gateway pass the original protocol and client address as
   forwarded headers.
3. In the facade, **trust those headers only when the request comes from
   the gateway's network**, identified by a fixed, configured range.
   Anything else must be ignored.
4. Make that configuration required: an invalid or missing trusted-range
   setting must refuse to start the process, never fall back to trusting
   everyone.
5. Test both directions: the header honored from inside the trusted
   range, and ignored from outside it.

**Done when:** both tests pass, the process refuses to start with a
broken trusted-range setting, and the session cookie's secure flag is now
genuinely true in a real browser.

**Common mistakes:**
- Trusting forwarded headers unconditionally. They're trivially forged,
  and your rate limiting (7.1) depends on the client address being real.
- Assuming your framework's built-in forwarded-header handling restricts
  by network correctly. Verify it with a request from outside the range;
  if it doesn't, implement the single check yourself.

---

## Phase 6 — Delete the stand-in

The shortest phase, and the one most often skipped. The Phase 1 stand-in
now has no reason to exist, and its removal criterion (0.4) is met.

### 6.1 Prove nothing calls it

**Do this:**
1. Search the codebase for callers. Check your frontend(s) specifically.
2. Check access logs for real traffic to it.
3. If something does still call it, fix that caller first. The real login
   flow has existed since Phase 2.

**Done when:** you can state, with evidence, that the only remaining
callers are its own tests.

### 6.2 Delete it, and everything that existed only for it

**Do this:**
1. Delete the endpoint and its request/response types.
2. Delete the authentication mechanism it introduced, if nothing else
   uses it — often the facade is left with exactly one authentication
   scheme, which simplifies everything downstream.
3. Delete modules/layers that are now empty. An empty layer is
   scaffolding protecting nothing; a future need can be designed around
   that need rather than this one's leftovers.
4. Delete its tests outright rather than disabling them.
5. Regenerate any committed generated artifacts (API schemas, clients)
   that referenced it.

**Done when:** the build is green, a full browser login still works end
to end, and nothing in the repository mentions the stand-in except your
decision record.

**Common mistakes:**
- Keeping it "for a future mobile client." Mobile uses the Phase 2 flow,
  launched in a system browser. This is not a reason.
- Keeping it "for future server-to-server calls." That's a genuinely
  different problem with different requirements; solve it when a real
  caller exists, against what the legacy system supports then.
- Leaving empty modules behind as placeholders.

### 6.3 Record what coverage you lost

**Goal:** honesty about the gap removal created.

**Do this:** if deleting those tests removed the only coverage of
something still in use (a common surprise), say so explicitly in the
commit or decision record, and decide whether to replace it now or accept
the gap deliberately.

**Done when:** the gap is either filled or written into the risk ledger.

---

## Phase 7 — Harden

These steps are largely independent of each other — parallelize freely.
Each ends in a test, because every one of them is a security control that
silently stops working if nothing watches it.

### 7.1 Rate-limit the unauthenticated entry points

**Do this:**
1. Apply a limit, per client address, to the login-start and callback
   endpoints.
2. Give the whole flow **one shared budget**, not one per endpoint —
   otherwise alternating between them doubles the effective limit.
3. Reject immediately when over budget (no queueing) and include a
   retry-after indication.
4. Make the limit configuration required and validated: degenerate
   values (zero permits, sub-second windows) must refuse to start.
5. Test: exactly at the limit passes, one more is refused; alternating
   endpoints can't exceed the shared budget; and an unrelated
   high-traffic endpoint is **not** caught by this policy.

**Done when:** all three test shapes pass.

### 7.2 Add circuit breakers around every call to the legacy system

**Do this:**
1. Identify every outbound call: the token exchange, the key-set fetch,
   and the proxy's forwarded calls.
2. Wrap each in a circuit breaker whose state is **shared across
   requests** — one long-lived instance per dependency. A breaker
   constructed per call can never trip.
3. Count only real failures: server errors and timeouts. An ordinary
   rejection (a bad credential, a not-found) must never count toward
   opening the circuit.
4. When open, fail fast with a generic error rather than hanging.
5. Test both: repeated server errors open it and stop calls reaching the
   legacy system; repeated ordinary rejections never open it, however
   many times they happen.

**Done when:** both tests pass, and you've verified the real behavior by
stopping the legacy system mid-session and observing a fast, clean
failure instead of a hang.

### 7.3 Add an audit trail

**Do this:**
1. Log, to one dedicated and stably-named category: login successes,
   login failures, proxied data access, and any admin action (3.4).
2. Record who, what, and when — never a credential, a token, or a
   request/response body.
3. For failures, log a short fixed internal label, not the legacy
   system's own error text.
4. Decide deliberately what you're *not* auditing, and write down why.
   A high-volume endpoint that reveals nothing sensitive is a reasonable
   exclusion; an unexamined one isn't.

**Done when:** a login, a proxied call, and an admin revocation each
produce exactly one audit entry with the expected fields, and 1.2/4.3's
tests still pass.

### 7.4 Make startup ordering explicit

**Do this:**
1. Have the facade wait for the legacy system's readiness before serving.
   Make this **fail-open**: if it times out, log a warning and start
   anyway — a host that never starts is worse than one that started slow.
2. Declare, in your orchestration, that the facade depends on: the
   legacy system being *healthy* (not merely started), and the
   provisioning steps (0.3, 3.2) having *completed successfully*.
3. Make the provisioning scripts themselves fail-closed: exit non-zero on
   timeout, so nothing proceeds on half-finished setup.

**Done when:** bringing up the entire stack from nothing, in one command,
reliably produces a working login — with no manual step and no
"run it again, it works the second time."

### 7.5 Sweep for fail-closed configuration

**Goal:** every security-relevant setting refuses to start rather than
degrading quietly.

**Do this:** list every configuration value that controls a security
decision — trusted network range, rate limits, admin allow-list,
certificate paths, redirect allow-list. For each, confirm: invalid values
refuse to start, and a missing value either refuses to start or defaults
to the *restrictive* option (empty allow-list = nobody, never everybody).
Add the startup test for each one.

**Done when:** each has a test asserting invalid configuration prevents
startup.

---

## Phase 8 — Prove it scales

Everything until now has quietly assumed one process. Find out what breaks
before an incident does.

### 8.1 Audit for per-process state

**Do this:** list everything that is generated per process or stored
locally — cryptographic key rings, certificates, interim protocol state,
caches, temp files. For each: is it now shared or persisted (3.2, 3.3), or
still per-process?

**Done when:** the list exists and every item is either shared/persisted
or explicitly accepted as a remaining blocker.

### 8.2 Actually run two instances

**Why this is its own step:** removing the blockers is not the same claim
as proving the result. Nobody has run two of these yet.

**Do this:**
1. Run two facade instances behind a load balancer with **no** session
   affinity.
2. Complete a login, confirming it works when the redirect back lands on
   a different instance than the one that started it.
3. Make authenticated requests repeatedly, confirming they succeed
   regardless of which instance serves them.
4. Revoke a session from one instance; confirm the other honors it
   immediately.

**Done when:** all four work. If any fail, the cause is almost certainly
something on 8.1's list.

**Common mistakes:**
- Enabling session affinity to make it pass. That hides the problem
  rather than fixing it, and a restart or rolling deploy reproduces it
  anyway. Affinity is an acceptable *documented interim measure*, never a
  fix.

### 8.3 Test a rolling deploy mid-flow

**Do this:** start a login, replace the instance handling it mid-flow,
then complete the login. Separately: hold an active session through a full
rolling replacement of every instance.

**Done when:** both survive. This is the test that distinguishes "works
with two instances" from "works in production."

---

## Phase 9 — Production gate

Do not treat "Phase 8 passed" as "ready for real users." Production adds
real secrets, real network exposure, real data, and real consequences.

The gate itself is a separate checklist — see the production readiness
section of the framework document. In summary, the categories that are
*not* covered by anything in Phases 0–8:

- Secrets from a real secrets manager, with rotation — not a local config
  file with shared credentials.
- Real certificates with a no-downtime rotation procedure.
- A genuine authorization model (what an identity may *do*), which this
  entire guide deliberately does not build.
- Metrics, tracing, and alerting — structured logs alone are not
  observability.
- Tested backup and restore of the session store.
- An incident runbook for a suspected compromised session.
- Resource limits derived from a real load test.
- Dependency and container image scanning in the pipeline.
- A security review of the *production* configuration specifically.
- A rollback plan covering active sessions and in-flight logins.

---

## Working practices throughout

Independent of phase:

- **Record each real decision as you make it**, briefly: what you chose,
  what you rejected, and why. The "why" is what makes it possible to
  revisit the decision intelligently later — and most of these decisions
  get revisited.
- **Keep a risk ledger from day one.** Every time you decide *not* to fix
  something, write it down with what would have to change for it to
  matter. A risk that's written down is owned; one that isn't is
  forgotten.
- **Prefer deleting to disabling.** Commented-out code, disabled tests,
  and unused endpoints all read as "someone might need this," which means
  nobody maintains them.
- **Verify by observation, not inspection.** Restart the process. Stop the
  dependency. Send the forged header. Reading configuration proves
  nothing about behavior, and this is a component where being wrong is
  expensive.
- **When something surprising works, find out why before moving on.** In
  this kind of integration, "it works now" and "I understand why it
  works" diverge often, and the gap always closes eventually — usually at
  an inconvenient moment.

## Signs you're off track

- **Your frontend has a token.** Go back to 2.4/2.5.
- **The stand-in is still there and Phase 2 works.** Go to Phase 6.
- **Restarting logs everyone out.** 3.3 isn't done.
- **"Revoke" only ends the caller's own session.** 3.4 isn't done.
- **A second instance breaks login intermittently.** 8.1's list has an
  unresolved item — most often the protocol library's interim state or the
  cookie key ring.
- **Setup needs a manual step, or works on the second attempt.** 7.4
  isn't done, and every new team member will hit it.
- **You're building an authorization model.** That's a different project.
  Make sure that's a deliberate decision, not scope drift.
