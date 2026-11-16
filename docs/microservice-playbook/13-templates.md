# 13. Templates

- **Part of:** the Microservice Migration Playbook ([README](README.md))
- **Use:** copy, fill in, delete the prompts. SQL is generic and must be adapted to your engine. Pseudo-code
  shows shape only.

---

## 1. Decision record

File: `docs/decisions/NNNN-short-title.md`. Never edit a decided record's outcome; supersede it.

```markdown
# NNNN. <Decision, phrased as the choice made>

- **Status:** Proposed | Accepted | Superseded by NNNN | Deprecated
- **Date:** YYYY-MM-DD
- **Builds on:** <links to earlier decisions or documents>
- **Rule(s):** <playbook rule ids this decision applies or deviates from>

## Context
What forced this decision? What is the situation, constraint, or finding? Cite evidence (file, query,
test). State what is unknown.

## Decision
Numbered, specific, testable statements of what we will do.

## Consequences
What becomes easier or harder? What must change elsewhere (code, grants, docs, roadmap)? What new risk
is accepted, and what is the compensating control?

## Alternatives considered
Each real option with the reason it lost. Include "do nothing".

## Revisit when
The observable event that should reopen this.

## Related decisions
Links.
```

Index file: one row per record: id, title, status, date. Update it in the same change.

---

## 2. Bounded-context one-pager

See [02](02-discovery-and-strategic-design.md) §5.

## 3. Glossary row

See [02](02-discovery-and-strategic-design.md) §6.

## 4. Rule catalogue row

| Id | Rule | Aggregate | Legacy source (evidence) | Parity / **(new)** | Enforced in | Test |
|---|---|---|---|---|---|---|
| PT-6 | First name is required, 1-255 characters | Patient | `Validator::configure()` line … | Parity | `Registration.create` | `PT_6_first_name_required` |

---

## 5. Ownership matrix row

| Table / resource | Service does | Source of truth | Other writers | Target owner | Grants | Failure behavior | Rollback |
|---|---|---|---|---|---|---|---|

## 6. Findings register row

| Id | Finding | Severity (C/H/M/L) | Effort (S/M/L) | Evidence | Fix | Depends on | Status |
|---|---|---|---|---|---|---|---|

## 7. Decision register row

| Id | Question | Options | Recommendation (default) | Blocks | Owner | Decided in |
|---|---|---|---|---|---|---|

## 8. Not-verified row

| Claim | Why unverified | How to verify | Owner | Done |
|---|---|---|---|---|

---

## 9. Slice plan (one page)

```text
SLICE:        <capability, verb phrase>
CONTEXT:      <bounded context>
WHY NOW:      <order justification, MIG-10>
LEGACY EVIDENCE: <files, queries, writers>
RULES:        <ids; parity vs (new)>
AGGREGATES / VALUE OBJECTS: <list>
PORTS:        <interfaces in domain language>
USE CASES:    <names>
DATA:         <tables read/written, grants, concurrency token, id allocation>
API:          <routes, status codes, idempotency, pagination>
EVENTS:       <names, same-transaction guarantee>
SECURITY:     <gate status, actor source, audit entries>
TESTS:        <domain, architecture, integration (as prod principal), pipeline, contract, failure>
DEPLOY:       <grants, config, route, container>
DOCS:         <decision records, glossary, matrix, changelog, roadmap>
EXIT:         <observable proof>
ROLLBACK:     <how to undo>
```

---

## 10. Code skeletons (pseudo)

### Value object and aggregate

```text
final class Registration {                       // value object
    private Registration(...) {}
    static Registration create(args) {
        errors = []
        // one rule per line, each with its rule id in a comment
        if (invalid(x)) errors += "Message identical to the edge validation message."
        if (errors.notEmpty) throw RegistrationInvalid(errors)
        return new Registration(...)
    }
}

final class Patient {                            // aggregate root
    private Patient(...) {}                      // no public constructor
    static Patient register(Registration r, long number, UserId by, Instant at) {...}
    internal static Patient reconstitute(row)  { ... }   // adapter only; no re-validation
}
```

### Port, use case, adapter

```text
interface PatientRegistry { void register(Patient p, PatientRegistered e); }   // Application.Ports

class RegisterPatient {                                                         // Application
    RegisterPatient(PatientRegistry registry, NumberAllocator numbers, Clock clock) {...}
    Patient execute(Input in, UserId by) {
        r = Registration.create(in...)
        p = Patient.register(r, numbers.next(), by, clock.now())
        registry.register(p, PatientRegistered.from(p))
        return p
    }
}

class SqlPatientRegistry implements PatientRegistry {                           // Infrastructure
    void register(Patient p, PatientRegistered e) {
        result = retryStrategy.execute(() => {          // whole unit of work (OPS-9)
            tx = db.begin()
            db.insert(rowFor(p)); db.insert(outboxRowFor(e)); audit.append(...)
            tx.commit(); return generatedId
        })
        p.assignGeneratedIdentity(result)               // once, after success
    }
}
```

### Architecture tests

```text
test Domain_references_no_other_module        : refs(Domain).filter(ours) is empty
test ReadModels_references_only_Domain        : refs(ReadModels).filter(ours).except(Domain) is empty
test Ports_reference_only_Domain_ReadModels   : ...
test Application_not_Infrastructure_or_Api    : ...
test Infrastructure_not_Application_or_Api    : ...
test Composition_not_Api_or_WebFramework      : ...
test InvariantTypes_have_no_public_ctor       : for T in InvariantTypes: publicConstructors(T) is empty
test InvariantTypes_have_no_public_setter     : for T in InvariantTypes: publiclySettable(T) is empty
```

### Endpoint

```text
group("/api/v1").requireAuthentication()                 // deny by default (SEC-1)
POST /patients (body, idempotencyKey, caller):
    try   { p = registerPatient.execute(body, caller.userId); return Created(location(p.uuid), toResponse(p)) }
    catch (RegistrationInvalid e)   { return BadRequest(problem(e.errors)) }
    catch (Conflict e)              { return Conflict(problem(e.message)) }
    // anything else -> global handler -> generic 500 problem body, logged
```

---

## 11. Owned tables (generic SQL)

```sql
-- Transactional outbox
CREATE TABLE service_outbox (
  id              BIGINT       NOT NULL AUTO_INCREMENT PRIMARY KEY,
  event_id        CHAR(36)     NOT NULL UNIQUE,
  event_type      VARCHAR(255) NOT NULL,
  schema_version  INT          NOT NULL,
  subject         CHAR(36)     NOT NULL,
  actor           VARCHAR(64)  NOT NULL,
  correlation_id  CHAR(36)     NULL,
  causation_id    CHAR(36)     NULL,
  payload         TEXT         NOT NULL,
  occurred_at     DATETIME     NOT NULL,        -- UTC
  published_at    DATETIME     NULL,
  attempts        INT          NOT NULL DEFAULT 0,
  last_error      TEXT         NULL,
  next_attempt_at DATETIME     NULL,
  dead            BOOLEAN      NOT NULL DEFAULT FALSE
);
CREATE INDEX ix_outbox_relay ON service_outbox (published_at, dead, next_attempt_at, id);

-- Relay claim (adapt: engine must support skip-locked row locking)
SELECT * FROM service_outbox
 WHERE published_at IS NULL AND dead = FALSE AND (next_attempt_at IS NULL OR next_attempt_at <= NOW())
 ORDER BY id LIMIT 100 FOR UPDATE SKIP LOCKED;

-- Append-only audit (grant INSERT only; no UPDATE or DELETE)
CREATE TABLE service_audit (
  id          BIGINT       NOT NULL AUTO_INCREMENT PRIMARY KEY,
  at          DATETIME     NOT NULL,            -- UTC
  actor       VARCHAR(64)  NOT NULL,
  actor_user  BIGINT       NULL,
  action      VARCHAR(100) NOT NULL,
  subject     CHAR(36)     NULL,
  outcome     VARCHAR(20)  NOT NULL,
  client_addr VARCHAR(64)  NULL,
  correlation CHAR(36)     NULL,
  details     TEXT         NULL                 -- minimal, non-sensitive
);

-- Idempotency
CREATE TABLE service_idempotency (
  idem_key    VARCHAR(100) NOT NULL,
  actor       VARCHAR(64)  NOT NULL,
  request_hash CHAR(64)    NOT NULL,
  response    TEXT         NOT NULL,
  status      INT          NOT NULL,
  created_at  DATETIME     NOT NULL,
  PRIMARY KEY (idem_key, actor)
);
```

---

## 12. Event envelope and error body

```json
{
  "eventId": "7c0e0a0e-...",
  "eventType": "PatientRegistered",
  "schemaVersion": 1,
  "occurredAt": "2026-11-16T09:30:00Z",
  "subject": "b1a9...-uuid",
  "actor": "user:42",
  "correlationId": "e3f2...",
  "causationId": "c9d4...",
  "payload": { "uuid": "b1a9...", "number": 1043, "firstName": "…", "lastName": "…" }
}
```

```json
{
  "type": "https://example.org/problems/validation",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "firstName": ["First name must be between 1 and 255 characters."] },
  "traceId": "e3f2..."
}
```

---

## 13. Idempotent provisioning job (shape)

```text
require env: DB_NAME, SVC_USER, SVC_PASSWORD, ADMIN_PASSWORD
run as admin against the database host:
    CREATE TABLE IF NOT EXISTS <owned tables>             -- or run versioned migrations (OPS-5)
    CREATE USER IF NOT EXISTS '<svc>';
    ALTER USER '<svc>' IDENTIFIED BY <quoted password>;   -- so rotation takes effect on rerun
    GRANT <verbs> ON <db>.<each table> TO '<svc>';        -- one commented line per grant (LEG-6)
    FLUSH PRIVILEGES;
print "ready" (never print secrets)
```

---

## 14. Pull request description

```markdown
## Problem
<what, why now, link to decision/issue>

## Approach
<what changed; what you chose not to do>

## Behavior or ownership change
<new rules (new), data written, grants, events, contracts>

## Contexts / APIs / events / migrations / controls touched
-

## Validation
<tests added; what was run (unit/integration/stack/manual); what was NOT run and why>

## Security and data
<gate status; actor source; audit; synthetic data only>

## Rollback
<how to undo; flag or route switch>

## Docs updated in this change
<decision records, glossary, matrix, changelog, roadmap>
```

## 15. Changelog entry

```markdown
# Changelog YYYY-MM-DD

## Added / Changed / Fixed / Removed — <short title>
- What changed, in user or operator terms.
- **Why**, with a link to the decision.
- **Verified:** how, and what was *not* verified.
```

## 16. Threat model one-pager

| Asset | Entry points | Spoofing | Tampering | Repudiation | Disclosure | DoS | Elevation | Controls (rule ids) | Residual risk / owner |
|---|---|---|---|---|---|---|---|---|---|

## 17. Runbook skeleton

```text
SERVICE:         <name>            OWNER / ON-CALL: <…>
HOW TO RESTART:  <steps>
HOW TO ROTATE A SECRET: <change config, re-run provisioner, verify>
HOW TO REPLAY DEAD LETTERS: <steps, safety checks>
HOW TO DISABLE THE NEW ROUTE (fall back to legacy): <steps, data reconciliation>
DEPENDENCIES AND FAILURE BEHAVIOR: <each: down means ...>
ALERTS AND WHAT THEY MEAN: <…>
KNOWN SHARP EDGES: <…>
```
