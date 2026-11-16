# 11. Checklists

- **Part of:** the Microservice Migration Playbook ([README](README.md))
- **Use:** copy a checklist into the pull request or milestone issue and tick it with evidence (a link, a
  test name, a command output). An unticked box needs a recorded exception.
- Each item cites the rule that explains it; follow the id when you need the *why*.

---

## A. Kickoff: before the first line of code

**Decisions** ([01](01-principles-and-decision-framework.md))
- [ ] KD-1 unit (module/library/service) decided, with the revisit trigger.
- [ ] KD-2 data location decided (shared legacy database, own, hybrid); consequences written.
- [ ] KD-3 trust and identity approach decided; identity-forwarding design exists (SEC-4).
- [ ] KD-4 sync/async split and event transport approach decided.
- [ ] KD-5 contract style decided (and any standards API planned).
- [ ] KD-6 database test pattern decided (disposable container; production principal).
- [ ] KD-7 parity vs improvement policy; "(new)" convention adopted.
- [ ] KD-8 exclusions and deferrals named, with preconditions.
- [ ] KD-9 deployment/environment model decided.

**Evidence** ([02](02-discovery-and-strategic-design.md))
- [ ] Legacy runs locally from a resettable snapshot (MIG-2).
- [ ] Schema inventory for every table in scope, including missing constraints (LEG §3).
- [ ] Legacy behavior read from source; rules have ids and citations (DSC-3, DSC-5).
- [ ] Every writer of the data identified (LEG-8).
- [ ] One-pager, glossary seed, context sketch, ownership matrix, decision list exist (DSC-1).
- [ ] Success-measure baselines recorded (DSC §8).

**Foundation** ([10](10-roadmap-and-milestones.md))
- [ ] Walking skeleton: stack, CI, conventions, decision folder, PR template (MIG-1).
- [ ] The first slice is chosen and is small, self-contained, and exercises every layer (MIG-6).

---

## B. New service skeleton

([03](03-hexagonal-service-architecture.md))
- [ ] Modules: Domain, ReadModels, Ports, Application, Infrastructure, Composition, API (ARC §2).
- [ ] Dependency tests (ARC-10) and the invariant test exist and run first in CI.
- [ ] Composition root: typed options, validate on start, adapters bound to ports (ARC-8, OPS-3).
- [ ] Authentication on by default on the route group (SEC-1) with a test (SEC-18).
- [ ] Global error handler and the standard error body (OPS-8, API-4).
- [ ] Contract types separate from the domain; serialized names pinned (ARC-7, API-2).
- [ ] Container build with restore layer and per-service ignore file (OPS-1); non-root (SEC-15).
- [ ] Compose service: no published port, depends on health and provisioning (OPS-2, OPS-6).
- [ ] Gateway route added; secret deleted-then-set per cluster (API-14, API-16).
- [ ] A "hello" path works end to end, including a database round trip as the production principal
      (QLT-8).

---

## C. New slice: definition of done

([08](08-quality-and-testing.md) §5)
- [ ] Research notes with evidence; writers listed (MIG §4 step 1).
- [ ] Rules specified with ids and sources; *(new)* items decided (DSC-5, DOM-15).
- [ ] Domain: factories validate and collect all errors; no public ctor/setter (DOM-2, DOM-3).
- [ ] Every write path goes through the aggregate; each path has a test (DOM-18, QLT-6).
- [ ] Identity allocation behind a port; time injected (DOM-7, DOM-8).
- [ ] Ports in domain language; use case is thin (ARC-3, ARC-4).
- [ ] Adapter: tolerant reads, strict writes, mapping round-trip tests (LEG-3, LEG-4, DOM-9).
- [ ] Transactions use the retry-safe pattern (OPS-9) and are tested under a forced fault (QLT-12).
- [ ] Integration tests as the production principal (LEG-7, QLT-8); grants updated and commented (LEG-6).
- [ ] API: shape validation + domain validation, same messages (ARC-11); status codes (API-3).
- [ ] Creating commands idempotent (API-5); lists paginated (API-6); no actor in the body (API-8).
- [ ] Event written in the same transaction for every state change (EVT-1, EVT-9).
- [ ] No post-commit work can change the response (EVT-15).
- [ ] API description correct (scheme, constraints); snapshot regenerated; client regenerated (API-9,
      API-10).
- [ ] Deployed: configuration documented, stack boots, smoke test passes (OPS-18, QLT-10).
- [ ] Documentation in the same change: decision record, glossary, ownership matrix, changelog,
      roadmap (P-13).
- [ ] Layered review done; findings fixed or tracked (MIG-12).
- [ ] Verification level stated honestly (QLT-15).

---

## D. Pull request

([13](13-templates.md) has the description template)
- [ ] Describes the problem, approach, and any behavior or data-ownership change.
- [ ] Names affected contexts, APIs, events, migrations, controls, and procedures.
- [ ] Includes tests, or says why none apply; states how it was validated.
- [ ] Uses synthetic or sanitized data only (QLT-24).
- [ ] Small enough to review as one thing.
- [ ] Two approvers if it touches: authentication, authorization, secrets, a public contract, a database
      migration, personal or financial data, or a production procedure.
- [ ] Rollback or flag details if behavior changes in production.

Review prompts: [08](08-quality-and-testing.md) §6.

---

## E. Security gates

([07](07-security-and-compliance.md) §9)

**Gate A: before any exposure**
- [ ] Deny by default on the route group (SEC-1).
- [ ] The edge verifies a signed-in user for protected prefixes; the secret is not the gate (SEC-2).
- [ ] Backends publish no port; trust header deleted-then-set per cluster; constant-time compare (SEC-3,
      API-15, API-16).
- [ ] Gate tests pass: no credential, wrong credential, direct access (SEC-18).

**Gate B: before the first write endpoint**
- [ ] Verified caller context; actor fields removed from bodies (SEC-4, API-8).
- [ ] Least-privilege principal and grants (LEG-6, SEC-6).
- [ ] Audit append-only, transactional, with verified actor (SEC-9).
- [ ] Input bounds, parameterized SQL, path containment (SEC-7).

**Gate C: before the first user interface**
- [ ] Gates A and B hold.
- [ ] An authorization policy port exists with default deny and object-level checks (SEC-5).
- [ ] Logs contain no secrets or bodies, tested (SEC-10).

**Gate D: before production**
- [ ] Secrets generated, scoped, and rotated by script; none committed (SEC-8).
- [ ] Supply-chain controls on (SEC-16); threat model dated (SEC-20); data duties planned (SEC-21).
- [ ] Rate limits on unauthenticated and expensive routes (SEC-13).
- [ ] Startup refuses missing secrets and dev bypasses outside Development, tested (SEC-14).

---

## F. Legacy integration gate

([05](05-legacy-integration-and-data.md) §12)
- [ ] Mapped legacy tables excluded from migrations (LEG-1).
- [ ] One ACL; per-column mapping documented (LEG-2).
- [ ] Encoding round-trip tests: identifiers, null/empty, dates (LEG-3).
- [ ] Malformed unrelated rows planted in tests (LEG-4).
- [ ] Every statement parameterized (LEG-5).
- [ ] Provisioning job idempotent and gating dependents (LEG-18, OPS-6).
- [ ] Concurrency token before any update command (LEG-10).
- [ ] Identity allocation strategy chosen, documented, and behind a port (LEG-9).
- [ ] Clock rule written and asserted at startup (LEG-11).
- [ ] Shared counter idiom pinned by a test (LEG-12).
- [ ] File handling: identity, containment, compensation, reconciliation (LEG-14).
- [ ] **Manually opened a service-created record in the legacy UI** (LEG-15).
- [ ] Legacy audit decision recorded (LEG-16).
- [ ] Rollback written and rehearsed (LEG-19).

---

## G. Event and outbox gate

([06](06-apis-gateway-and-events.md) §7)
- [ ] Event row in the same transaction as the change (EVT-1).
- [ ] Envelope has id, type, version, time, subject, actor, correlation, causation (EVT-2).
- [ ] The relay exists: ordered, locked, back-off, dead-letter, purge, alert (EVT-7).
- [ ] Per-subject ordering preserved (EVT-4).
- [ ] Consumers idempotent on the event id (EVT-3).
- [ ] Payload curated and privacy-reviewed (EVT-5).
- [ ] Transport behind a port; consumers never read the outbox (EVT-8).
- [ ] Every state-changing command emits (EVT-9).
- [ ] Consumer-driven contract test exists (EVT-10).
- [ ] The outbox is not used as the audit (EVT-12).

---

## H. Production readiness

([09](09-delivery-and-operations.md) §12)
- [ ] Clean checkout → running stack in one command (OPS-17, OPS-20).
- [ ] Missing configuration fails at startup, tested (OPS-3).
- [ ] Health and readiness endpoints in use (OPS-7).
- [ ] Two instances and a rolling deploy verified (OPS-19).
- [ ] Breakers, timeouts, retries documented and tested (OPS-9, OPS-10).
- [ ] Structured, correlated, clean logs; alerts for the OPS-11 signals.
- [ ] Backups restored successfully recently; retention defined (OPS-15).
- [ ] Rollback written and rehearsed (OPS-14).
- [ ] Runbook: restart, rotate a secret, replay dead letters, disable the route (OPS-16).
- [ ] Security Gate D passed.
- [ ] No open critical or high findings (MIG-8).

---

## I. Milestone exit

([10](10-roadmap-and-milestones.md) §7)
- [ ] Exit criteria met with evidence.
- [ ] Layered review done; findings fixed or tracked (MIG-12).
- [ ] Findings and decision registers current.
- [ ] Success measures re-baselined (QLT §9).
- [ ] Roadmap and changelog updated in the same change.
- [ ] Decision gate consciously passed (MIG-5).

---

## J. Cutover

([05](05-legacy-integration-and-data.md) §11)
- [ ] Parity evidence from characterization tests and side-by-side runs (QLT-14).
- [ ] Staged routing plan; each stage has verify and rollback steps.
- [ ] Rollback rehearsed before step one (MIG-15).
- [ ] Legacy write path closed only after parity (LEG-8, LEG-19).
- [ ] Support briefed; monitoring watches the new path.
- [ ] Decommission evidence gathered before removal (MIG-16).

---

## K. Retrospective (after each milestone)

- [ ] What surprised us? Add it to [12](12-lessons-learned.md) **and** attach it to a rule.
- [ ] Which rule was ignored, and why? Was it wrong, or inconvenient?
- [ ] Which finding aged badly (stayed open too long)? What blocks it?
- [ ] Which estimate was off most, and what does that say about the next one (MIG-9)?
