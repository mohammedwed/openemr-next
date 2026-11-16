# Microservice Migration Playbook

A framework-neutral guide to **extracting a domain service from a legacy system and building it with
Domain-Driven Design (DDD) and hexagonal architecture**, one verifiable slice at a time.

- **Status:** Draft v1
- **Written:** 2026-11-16
- **Derived from:** building one real service (a "Patient Management" service) next to a legacy PHP
  monolith, including the mistakes. Nothing here depends on that domain, language, or framework.
- **Purpose:** be the reference that lets you, or someone who never met this project, reproduce the result
  and avoid the detours.

## The one-paragraph version

Pick a **bounded context** with a clear business meaning. Study the legacy system until you can state, with
evidence, what it owns, what rules it enforces, and which of its data you may touch. Build a **vertical
slice** (domain rule, use case, adapter, API, tests, deployment, docs) that exercises every layer and the
real deployment path. Keep the domain **free of every technology**, and make the build **fail** if that
rule is broken. Reach the legacy data through an **anti-corruption layer** that is tolerant when reading
and strict when writing. Put **security, audit, and failure behavior** in the first slice, not the last.
Record every non-obvious decision, with its alternatives, **in the same change** that implements it.
Repeat per slice, closing the gaps in your own register as you go.

## Who should read what

| You are | Read first | Then |
|---|---|---|
| **Architect / tech lead starting a migration** | 01, 02, 10 | 05, 07, 12 |
| **Developer building a slice** | 03, 04, 10 (slice cycle) | 05, 06, 08, 11 |
| **Developer adding a second service** | 01 (KD-1, KD-5), 06, 10 | 12, 14 |
| **Security reviewer** | 07, 12 | 05, 06, 11 |
| **QA / reviewer** | 08, 11 | 04, 12 |
| **Someone reproducing this repo** | 14 | everything it points to |
| **In a hurry** | 12 (lessons), then 11 (checklists) | |

## The documents

| # | Document | Answers | Rule prefix |
|---|---|---|---|
| 01 | [Principles and decision framework](01-principles-and-decision-framework.md) | Why this approach? Which big decisions must be made first, and how? | `P-`, `KD-` |
| 02 | [Discovery and strategic design](02-discovery-and-strategic-design.md) | How do I learn the legacy system and define the boundary? | `DSC-` |
| 03 | [Hexagonal service architecture](03-hexagonal-service-architecture.md) | What are the layers, and how are they enforced? | `ARC-` |
| 04 | [Tactical domain modeling](04-tactical-domain-modeling.md) | How do I model aggregates, rules, and identity? | `DOM-` |
| 05 | [Legacy integration and data](05-legacy-integration-and-data.md) | How do I share, protect, and eventually own legacy data? | `LEG-` |
| 06 | [APIs, gateway, and events](06-apis-gateway-and-events.md) | How do clients and other services talk to it? | `API-`, `EVT-` |
| 07 | [Security and compliance](07-security-and-compliance.md) | How is it made safe, and what did we get wrong? | `SEC-` |
| 08 | [Quality and testing](08-quality-and-testing.md) | How do I know it is correct and stays correct? | `QLT-` |
| 09 | [Delivery and operations](09-delivery-and-operations.md) | How is it built, configured, deployed, and kept running? | `OPS-` |
| 10 | [Roadmap and milestones](10-roadmap-and-milestones.md) | In what order, with what exit criteria? | `MIG-` |
| 11 | [Checklists](11-checklists.md) | What do I tick before I say "done"? | |
| 12 | [Lessons learned](12-lessons-learned.md) | What went wrong, and which rule prevents it? | |
| 13 | [Templates](13-templates.md) | What do I copy to start each artifact? | |
| 14 | [Reference implementation map](14-reference-implementation-map.md) | Where is each rule realized in this repository? | |

## Conventions

**Normative words.** **MUST** and **MUST NOT** are not negotiable without a recorded exception (see
"Exceptions" below). **SHOULD** and **SHOULD NOT** are defaults; deviate with a stated reason. **MAY** is a
permitted option.

**Rule ids.** Every rule has a permanent id (`ARC-3`). Checklists, pull requests, and code reviews cite ids.
A retired rule keeps its id and is marked *Retired*. Ids are never reused.

**Rule shape.** Each rule is a statement followed by *Why* (the rationale you can argue with) and, where it
helps, *How* and *Check* (how to verify it mechanically or by review).

**Status words** used when describing a system's state, so claims stay honest:

| Word | Meaning |
|---|---|
| **Built** | Code and a test exist and match the description. |
| **Target** | A proposal. Not built. |
| **Deferred** | A deliberate wait on something external, with a stated precondition. |
| **(new)** | A rule that changes behavior relative to the legacy system. Needs an explicit decision. |
| **Unverified** | Rests on reading, not on running anything. Listed, never hidden. |

**Neutral vocabulary.** Examples use plain words (*the ORM*, *a reverse proxy*, *a message broker*,
*a disposable database container*). Where a concrete product matters, only
[14](14-reference-implementation-map.md) names it.

**Pseudo-code** uses a Java-like, language-neutral style. It shows shape, not syntax to copy.

## Assumptions

This guide assumes:

1. The legacy system has a **relational database** you can reach, and source code or documentation you can
   read.
2. The new services are **networked services** that speak HTTP and may exchange asynchronous events.
3. A **small team** (one to a few people) works in short-lived branches and reviews every change.
4. The data is **sensitive** (health, financial, or personal). Where a rule is only needed for sensitive
   data, it says so; otherwise treat the rule as the default.
5. You can run the legacy system **locally from a snapshot**. If you cannot, fix that first (MIG-2).

If an assumption is false, the affected rules say what to change.

## Exceptions

An exception to a MUST is recorded in a decision record (template in [13](13-templates.md)) with: the rule
id, the reason, the risk, the owner, the compensating control, and a review date. "Inconvenient" is not a
reason. An exception without a review date is a permanent decision in disguise; record it as one.

## What this guide is not

- **Not a framework or library.** It prescribes structure and discipline, not code to import.
- **Not a mandate to build microservices.** [01](01-principles-and-decision-framework.md) starts by
  asking whether you should. A module in the existing system is often the right first answer.
- **Not complete for authentication migration.** That is a distinct problem with its own documents in this
  repository (`docs/legacy-auth-migration-handbook.md` and companions); [07](07-security-and-compliance.md)
  covers only how a *domain service* consumes an identity.
- **Not a substitute for domain knowledge.** The process gives you ways to find the rules; it cannot tell
  you which ones a clinician, accountant, or operator actually relies on. Ask them.

## Keeping this guide current

1. A lesson from a real incident or review is added to [12](12-lessons-learned.md) **and** turned into or
   attached to a rule. A lesson with no rule will recur.
2. A rule that proves wrong is **retired with a note**, not deleted.
3. Review the whole guide at the end of each migration phase ([10](10-roadmap-and-milestones.md)).
4. [14](14-reference-implementation-map.md) is updated whenever a rule's realization in this repository
   moves, so reproduction stays possible.
5. Changes to this guide follow the same rule as code: document in the same change.

## Relationship to existing documents in this repository

| Existing document | Relationship |
|---|---|
| `docs/domain-driven-design.md` | The project's own DDD method. This playbook generalizes and extends it. |
| `docs/openemr-ddd-design.md` | The domain blueprint for the whole system. Project-specific. |
| `docs/patient-management/` | The full design set for the first service: the worked example of doc 02's artifacts. |
| `docs/architecture/` | The decision records. Doc 14 cites them. |
| `docs/legacy-auth-*` and `docs/auth-facade-implementation-guide.md` | The same three-document pattern (framework, standalone, build order) applied to authentication. |
| `docs/development-workflow.md` | Branching, review, and CI rules this playbook assumes. |
