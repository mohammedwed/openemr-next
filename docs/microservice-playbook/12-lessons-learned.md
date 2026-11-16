# 12. Lessons learned

- **Part of:** the Microservice Migration Playbook ([README](README.md))
- **Read this if:** you want to avoid our mistakes. If you read one document, read this and
  [11](11-checklists.md).
- **Source:** the first service's code, decision records, review notes, and findings register. Each lesson
  names the rule that now prevents it.

Each lesson is shown as **what happened → why it happened → what prevents it**. Severity: **C** critical
(exposes or loses data), **H** high (wrong results or compliance gap), **M** medium (blocks work or likely
defect), **L** low.

## 1. Security and identity

| ID | Sev | What happened | Why | Prevention |
|---|---|---|---|---|
| **L-1** | **C** | The gateway attached the backend's shared secret to **every** request, so the patient API was reachable by anyone, signed in or not. | "Gated by the secret" was read as "gated". A secret proves provenance, not that a user is signed in. The identity check was deferred as "not buildable yet", and the deferral had no deadline. | SEC-2, SEC-3, SEC-18, KD-3 |
| **L-2** | H | The acting user (`registeredBy`, `ownerId`, `authenticatorUserId`) was whatever the client typed. | The identity component produced only an external UUID; the legacy columns needed integer ids; no mapping existed, so the field stayed client-supplied. | SEC-4, API-8, design the id mapping early |
| **L-3** | H | No authorization policy: any caller could act on any record. | Treated as part of the deferred identity work. | SEC-5 |
| **L-4** | H | Nothing recorded who changed or read what (0 of 5 commands audited); the legacy audit log was bypassed. | Audit was not in the first slice's definition of done. | SEC-9, LEG-16, P-9 |
| **L-5** | H | A full frontend was built on top of the ungated service, and then the gap was documented. | Slice order put UI before the security gate; the caveat was recorded, not enforced. | SEC-22, MIG-8, MIG-13 |

**The pattern.** A *named deferral* felt like a control. It is not one. If you defer a security control,
write the date it blocks something ("no user interface until closed") and make a test fail when someone
works around it.

## 2. Data and legacy integration

| ID | Sev | What happened | Why | Prevention |
|---|---|---|---|---|
| **L-6** | M | "We are the only writer" was false: the legacy REST API, enabled in the stack and reachable through the browser proxy, still inserted rows. | The team retired the legacy *screen* and stopped looking for other write paths. | LEG-8, DSC §3 |
| **L-7** | H | Patient numbers were random 63-bit values generated inside the aggregate. A collision was a raw 500; the numbers broke the legacy "max plus one" allocator. | The unique constraint made random numbers look safe; the shared number space was not analyzed. | DOM-7, LEG-9 |
| **L-8** | M | A UUID stored as 16 bytes in a different byte order than the platform's native layout would have silently stored a *different* UUID than legacy reads back. | Encodings differ between platforms and legacy code. | LEG-3 (round-trip test through the real column) |
| **L-9** | M | A value converter did not run when the value was null, causing a `NOT NULL` violation against a real database. | Some mappers skip converters for null; the column was `NOT NULL DEFAULT ''`. | DOM §6 (backing-field mapping), LEG-3 |
| **L-10** | H | Integration tests passed, but the real stack failed: the least-privilege user lacked `SELECT` on a counter table (`UPDATE` with a self-referencing `SET` needs it). | The test fixture connected as a more permissive account. | LEG-7, QLT-8 |
| **L-11** | M | A strict date read threw on legacy zero/partial dates. It was "safe" while only one path used it; reusing it from a second path made one unrelated bad row break every new add for that patient. | Reuse changed an assumption silently. | LEG-4, DOM-9; plant malformed unrelated rows in tests |
| **L-12** | M | Rows created by the service (a policy without subscriber data; "updated by" empty) may not open in legacy screens. **Never checked.** | Compatibility was assumed; no one opened the old UI. | LEG-15 (manual check first) |
| **L-13** | L-M | A file and its database row are not atomic: a crash between them leaves an orphan; the compensating delete is untested because the failure is hard to force. | Two stores, no shared transaction. | LEG-14 (compensate, then reconcile) |
| **L-14** | M | The outbox table was created with `CREATE TABLE IF NOT EXISTS` (cannot evolve) and granted `ALL PRIVILEGES`. | Provisioning script written for creation only. | OPS-5, LEG-6 |
| **L-15** | M | The database stores UTC in some columns and server-local time in others; nothing asserts they agree. | The legacy mix was not documented or checked. | LEG-11 |

## 3. Reliability and eventing

| ID | Sev | What happened | Why | Prevention |
|---|---|---|---|---|
| **L-16** | H | Registration committed, then ran duplicate scoring **inline**; a scoring error returned 500 for a created patient, and the client's retry registered the same person again. | Follow-up work was placed after the commit in the request path; no idempotency. | EVT-15, API-5, EVT-11 |
| **L-17** | M | The milestone exit criterion said the event "is published exactly once". The outbox existed; **nothing read it**. | "Built" meant "table exists", not "event arrives". | EVT-7, P-10 (Built = works end to end) |
| **L-18** | M | Only registration wrote an event. Insurance and document changes opened their own transactions and wrote none. | The pattern was applied to the first command only. | EVT-9 |
| **L-19** | H (averted) | Enabling transient-fault retry without restructuring would have broken every explicit transaction, **but only on the first retry that actually fired**; a retried block that assigned a generated id to an aggregate would also have thrown on its second attempt. Found by analysis and fixed *before* it shipped; then verified against a real database. | The library requires the whole unit of work inside the retried block, and side effects inside it re-run. Ordinary tests never trigger a retry. | OPS-9, QLT-12 |
| **L-20** | M | No global exception handler existed in any service; unexpected failures produced a bare 500. (Fixed for one service; the others still lack it.) | Each endpoint handled its own known errors; nobody owned "everything else". | OPS-8, API-4 |
| **L-21** | M | Conflicts returned as 400; list endpoint returned everything; no idempotency key. | Contract details deferred as "polish". | API-3, API-5, API-6 |

## 4. Design and process

| ID | Sev | What happened | Why | Prevention |
|---|---|---|---|---|
| **L-22** | M | An identity bridge was built with five layers, then three were deleted along with the stand-in they served. | Layering was applied by habit to a component with no domain; a temporary stand-in lingered. | ARC-12, MIG-11 |
| **L-23** | M | A use case called the entity factory directly and bypassed the cluster aggregate, so the cluster invariant was reachable from one path only; a database constraint hid it until a caller got a raw error. | Two paths, one rule, one test. | DOM-18, QLT-6 |
| **L-24** | M | The swap decision lived in the infrastructure adapter and was testable only with a real database. | Logic and persistence were written together. | DOM-13 (decision in the aggregate; workaround in the adapter) |
| **L-25** | M | A hand-rolled standards-API resource shipped with the wrong coded-value type for two extensions, found later by re-reading legacy. A replacement library's validator did not work with the newest model generation; **discovered only by running it**. | Release notes were trusted; the standard is large. | API-11 (library, empirical versions, vendored profiles) |
| **L-26** | L | Domain types echoed the wire standard's field names; read models sat in the same module as aggregates. | Convenience. | DOM-14, ARC-9 |
| **L-27** | M | Every identifier was a plain integer, so two arguments could be swapped silently. | Not seen as urgent. | DOM-4 |
| **L-28** | H | The full design set was written **after** the build and found 37 findings in shipped code, including L-1. | Design was treated as documentation of the code, not a guide for it. | DSC-1, MIG-3 |
| **L-29** | M | Some work was "build-verified only"; a phone-width check could not actually resize the viewport; several design claims were from reading, not running. | Verification level was not recorded. | QLT-15, DSC-3, DSC §9 |
| **L-30** | L | A caveat ("actor is caller-supplied, not verified") appeared on one request type and was missing on three identical ones. | Manual, local documentation. | QLT §4 (documentation dimension: grep for repeated caveats) |
| **L-31** | L | A database-server version literal was hard-coded in three places. | No single source. | QLT §4 (hard-coded-values audit) |

## 5. Packaging, tooling, and tests

| ID | Sev | What happened | Why | Prevention |
|---|---|---|---|---|
| **L-32** | M | The container build failed with an "asset not found" error although restore had succeeded: the host's `bin/obj` folders were copied over the container's. | A per-service build context needs its own ignore file; the repo-root one does not apply. | OPS-1 |
| **L-33** | M | The container could not read the shared documents volume. | The volume belonged to one user id with mode 700. | SEC-15, OPS-2 |
| **L-34** | M | The generated API description declared no security scheme and showed no validation constraints; one numeric bound was published as a rounded floating-point value. | Attributes on constructor *parameters* did not reach the schema generator; the range attribute stores bounds as doubles. | API-9 (test the real generated document) |
| **L-35** | L | Two generated clients each defined a class with the same name; pages using both were ambiguous. | No naming strategy for generated code. | API-10 |
| **L-36** | M | A duplicate-detection test failed intermittently, passing on rerun, because "unique" last names shared a phonetic code. | Uniqueness was assumed under string equality, but the rule used a phonetic function over a short prefix. | QLT-4, QLT-22 |

## 6. The meta-lessons

1. **A deferral is not a control.** Give it a trigger and a test, or close it. (L-1, L-5)
2. **"Built" means it works end to end.** A table is not an event bus; a header is not a login. (L-1,
   L-17)
3. **Anything that runs only on failure must be tested by failing.** Retries, compensations, error
   handlers. (L-19, L-13, L-20)
4. **Test as production.** Same principal, same schema, same engine. (L-10, L-12)
5. **Reuse changes assumptions.** When a method gains a second caller, re-read its preconditions.
   (L-11, L-23)
6. **Look for the second writer.** (L-6)
7. **Write design first, thinly.** Late design finds expensive problems. (L-28)
8. **Say how you verified.** (L-29)
9. **Fix at the lowest layer that makes the bug impossible.** A type or a domain invariant beats a check in
   the endpoint. (L-23, L-27)
10. **Documentation drifts unless it ships with the change.** (L-30, P-13)

## 7. How to use this list

- At kickoff, read sections 1 to 3 and decide which you are exposed to.
- At each milestone's review ([08](08-quality-and-testing.md) §4), scan for lookalikes in your service.
- When you find a new problem, add it here **and** attach it to a rule ([README](README.md) "Keeping this
  guide current").
