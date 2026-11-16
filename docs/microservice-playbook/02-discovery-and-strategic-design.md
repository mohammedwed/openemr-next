# 02. Discovery and strategic design

- **Part of:** the Microservice Migration Playbook ([README](README.md))
- **Read this if:** you must define a bounded context, or you are about to design a service and need to
  know what to learn first.
- **Rule prefix:** `DSC-`

## 1. What this phase produces, and why

Strategic design answers four questions before any code: **What is this context for? What does it own?
What does it call and who calls it? Which words mean what?** The output is a small **design set** that the
code is then written against, and checked against afterwards.

> **Lesson.** In the source project the design set was written *after* the first slice was built. It found
> 37 findings in code that already shipped, including a critical security hole and a defect that
> committed data and then reported failure. Written earlier, most of those findings would have been design
> review comments. **Write a thin version first** (DSC-1), and deepen it as the slice grows.

## 2. Rules

**DSC-1 — Write a thin design set before the first slice, and deepen it with each slice.**
Minimum before coding: the bounded-context one-pager (§5), a glossary seed (§6), a context sketch (§7), the
data-ownership matrix (§9), and the decision list of KD-1…KD-9 ([01](01-principles-and-decision-framework.md)).
*Why:* these are the artifacts whose absence causes rework: boundary arguments, naming drift, and
surprise data coupling.

**DSC-2 — Start from capabilities and workflows, never from tables.**
*Why:* a table is an implementation of a decision someone made years ago, possibly for reasons that no
longer hold. A table often mixes several concepts (a "patient" row holding identity, demographics,
contact data, and flags). Boundaries drawn along tables reproduce the legacy coupling.

**DSC-3 — Every claim about legacy cites evidence.**
A file, a function, a table, a query result. Claims you did not run are listed as **Unverified**
(README status words), with how to verify them.
*Why:* the legacy system is the one source of truth about current behavior, and your memory of it is not.

**DSC-4 — Write down what the context does *not* own.**
*Why:* scope creep enters as "while we are here". An explicit non-ownership list is the cheapest defense and
the first thing a new teammate should read.

**DSC-5 — Give every business rule a permanent id.**
Prefix by aggregate or area (`PT-3`, `IC-8`). Tests cite the id in their name; the design set links rule to
evidence; a removed rule keeps its id marked *Retired*.
*Why:* traceability from legacy behavior → rule → test is what lets you answer "did we preserve this?"
without re-reading everything. (The source project created ids late and consequently could not show which
of 51 rules had a test that named it.)

**DSC-6 — Separate the *language* from the *schema*.**
Keep a glossary with, per term: definition, owning context, accepted synonyms, **prohibited ambiguous use**,
legacy equivalent, code representation, and wire representation. Record the places language and code
disagree.
*Why:* one word will mean two things in two contexts (a "session", a "user", an "encounter"). Forcing one
meaning across contexts creates the ambiguity the glossary exists to remove.

**DSC-7 — Decide the context relationships explicitly.**
For each neighbor, name the relationship pattern (customer-supplier, conformist, anti-corruption layer,
published language, shared kernel, partnership), who leads, and what crosses the boundary.
*Why:* an unnamed relationship defaults to "whoever changes first breaks the other".

**DSC-8 — Keep the shared kernel tiny.**
Share only identifiers and a few value types. Everything else is duplicated on purpose.
*Why:* a large shared kernel is a monolith with extra steps: every change needs every team.

**DSC-9 — Build a findings register and a decision register, and keep both current.**
Findings: id, severity, effort, fix. Decisions: id, options, recommendation, default, what it blocks.
*Why:* it converts "things we know are wrong" from folklore into a prioritized plan, and every deferral
becomes visible.

**DSC-10 — Exclude and defer by name, with preconditions.**
*Why:* see KD-8. Record the excluded context (and where it belongs) and the deferred capability (and what
must exist first).

**DSC-11 — Choose the first slice for learning value, not size.**
*Why:* see MIG-6. The first slice should be small, self-contained, and touch every layer, including the
event path and the deployment path.

## 3. The discovery process, step by step

### Step 1. Get an evidence base

1. **Run the legacy system** locally from a snapshot you can reset (MIG-2).
2. **Inventory the schema** ([05](05-legacy-integration-and-data.md) §3): for each relevant table, columns,
   types, nullability, defaults, keys, unique constraints, foreign keys, triggers. Note **missing**
   constraints; they decide your integrity rules and your concurrency risks. (Example finding: a core table
   with no primary key and no foreign keys anywhere.)
3. **Read the legacy source for behavior**, not just structure: the service classes, validators, and
   controllers that read and write the capability. Extract: validation rules, identifier generation,
   computed values, side effects, error behavior.
4. **Survey the real data** (on synthetic or sanitized copies): value distributions, zero/partial dates,
   empty-versus-null conventions, free-text fields that are really enums. *Why:* the dirty data you find
   here is the dirty data your read path must tolerate (LEG-4).
5. **Find every writer** ([05](05-legacy-integration-and-data.md) LEG-8): UI screens, REST/API endpoints,
   batch jobs, import tools, database triggers, other modules.
6. **Record naming traps**: columns whose name lies (a "date" that is really "last updated"), identifiers
   that look unique and are not, two ids for one thing.

*Time-box:* a few days per context for the first pass; deepen per slice.

### Step 2. Define the context

Write the one-pager (§5). Check it against the capability list: every capability is owned by exactly one
context, or explicitly "not here". Classify the context:

| Class | Meaning | Implication |
|---|---|---|
| **Core** | Differentiating, highest business value | Invest in the model; build, do not buy |
| **Supporting** | Needed, not differentiating | Simpler model; may be bought or minimal |
| **Generic** | Commodity (identity, notifications) | Buy or adopt a standard |

### Step 3. Build the language

Seed the glossary from the legacy code, the screens, and the people. Resolve conflicts by assigning terms to
contexts. Add prohibited usages. Revisit it in every slice (changed terms update the glossary in the same
commit).

### Step 4. Map the context

Draw the neighbors (§7). For each relationship: pattern, direction, what crosses (commands, queries,
events, shared data), the contract, and the risk. Name the **anti-corruption layers**.

### Step 5. Model the domain

Aggregates, entities, value objects, rules, domain services, invariants ([04](04-tactical-domain-modeling.md)).
Keep aggregate boundaries around **consistency requirements**, not around data you happen to load together.
Mark each element Built or Target.

### Step 6. Enumerate behavior

Commands, queries, workflows, and events ([06](06-apis-gateway-and-events.md)). For each workflow note the
unhappy paths and what happens when a step fails after a commit.

### Step 7. Map data ownership

For every table the service touches: read, written, by whom, source of truth, who else writes, target owner,
failure behavior, rollback ([05](05-legacy-integration-and-data.md)).

### Step 8. Consolidate gaps and plan

Findings register, dependency graph, phased evolution plan, decision register, what is unverified, success
measures (§8).

### Step 9. Diagram

Use cases, context map, class model, sequence diagrams for the workflows that cross a boundary or have a
failure mode, state diagrams for lifecycles, an ER view of owned data, and an event flow. Diagrams as text
(Mermaid or similar) so they are reviewed like code.

## 4. The design set: files and what each must contain

| # | Artifact | Minimum content |
|---|---|---|
| 01 | **Bounded context** | Purpose, users, capability list (Built/Target/Deferred), owns, does not own, legacy tables, classification, differences from earlier sketches |
| 02 | **Ubiquitous language** | Glossary (DSC-6), prohibited usages, places language and code disagree |
| 03 | **Context map** | Map, relationships (DSC-7), integration contracts, decisions needed, risks |
| 04 | **Domain model** | Aggregate catalogue, per-aggregate state/behavior/lifecycle/invariants, rule catalogue (DSC-5), value objects, domain services, design findings, enforcement |
| 05 | **Workflows and events** | Command and query catalogue, workflows with failure analysis, event catalogue, envelope, outbox and relay, consistency summary |
| 06 | **Data ownership and legacy mapping** | Ownership matrix, per-table detail, grants, identity allocation, concurrency, time, failure, audit, rollback |
| 07 | **Gaps and evolution** | Findings (DSC-9), dependency graph, phased plan, decision register, not-verified list, success measures |
| 08 | **Diagrams** | Section 3 step 9 |

Templates: [13](13-templates.md).

## 5. The bounded-context one-pager

```text
NAME:            <ubiquitous-language name, not a table name>
CLASS:           Core | Supporting | Generic       (and the reason)
PURPOSE:         one sentence in the business's words
USERS / ACTORS:  who invokes it, who consumes its output
CAPABILITIES:    C1 ... Cn, each Built | Target | Deferred | Excluded
OWNS:            data, rules, decisions this context is the source of truth for
DOES NOT OWN:    what it must refuse to take on (and who does)
LEGACY MAPPING:  tables, screens, APIs involved today
DEPENDS ON:      upstream contexts and what it needs from each
PUBLISHES:       events and query contracts it offers
SECURITY / AUDIT / CONTINUITY: the non-negotiables for this data
DIFFERENCES FROM EARLIER SKETCHES: and why
```

## 6. Glossary row

| Term | Definition | Owning context | Synonyms | **Do not use for** | Legacy equivalent | In code | On the wire |
|---|---|---|---|---|---|---|---|

Rule of thumb: if two people on the team give different definitions of a word, it belongs in this table
with a ruling.

## 7. Context relationships: the patterns

| Pattern | Meaning | When to choose |
|---|---|---|
| **Customer–supplier** | Downstream negotiates needs; upstream commits | You control both sides |
| **Conformist** | Downstream accepts upstream's model as-is | Upstream will not change and its model is acceptable |
| **Anti-corruption layer (ACL)** | Downstream translates upstream's model into its own | Upstream's model is legacy or foreign; **default for legacy** |
| **Published language** | A documented, versioned contract (events, an API, a standard) | Many consumers |
| **Shared kernel** | A small model both sides own together | Rare, and tiny (DSC-8) |
| **Partnership** | Teams succeed or fail together | Two contexts co-evolving |

For a legacy system, the relationship is nearly always: **new service → ACL → legacy**.

## 8. Success measures (write them down now)

Baseline now, target later; remeasure at each phase ([08](08-quality-and-testing.md) §9).

| Measure | How to count |
|---|---|
| Legacy table/column names inside the domain | Count of references; must stay 0, enforced by a test |
| Business rules with a test that names the rule id | n of N |
| Aggregates whose invariants cannot be bypassed (no public constructor/setter), test-enforced | n of N |
| Commands that commit their event atomically | n of N |
| Commands whose acting user is verified | n of N |
| Commands that write an audit entry | n of N |
| Integration tests run as the production database principal | yes/no |
| Records creatable with no sign-in through the public edge | must be no |

## 9. Evidence discipline: the "not verified" list

Keep a table at the end of the findings document:

| Claim | Why unverified | How to verify |
|---|---|---|
| "Legacy screens open a record this service created" | Not exercised | Manual check in the legacy UI |
| "Both clocks are UTC in the stack" | Not asserted anywhere | Read the server time zone |

*Why:* this converts uncertainty from a hidden risk into a task list, and keeps you from mistaking
reading for knowing.

## 10. Common mistakes in this phase

| Mistake | Effect | Prevention |
|---|---|---|
| Starting from the schema | Boundaries replicate legacy coupling | DSC-2 |
| Design set written after the code | Drift; findings arrive when expensive | DSC-1 |
| One big glossary for the whole system | Meanings blur | DSC-6 (per context) |
| Assuming single writer | Silent corruption, surprise rows | LEG-8 |
| Scope by omission | Rework, arguments | DSC-4, DSC-10 |
| Reading code, never running it | Wrong rules recorded as fact | DSC-3 |
| Ids and statuses added late | No traceability | DSC-5 |

## 11. Checklist

- [ ] Legacy runs locally from a resettable snapshot.
- [ ] Schema inventory done for every table in scope, missing constraints noted.
- [ ] Legacy behavior read from source; rules extracted with ids and evidence.
- [ ] Real-data survey done on safe data; dirty-data rules noted.
- [ ] Every writer of the data is listed.
- [ ] One-pager states owns and does-not-own.
- [ ] Glossary has prohibited usages and legacy equivalents.
- [ ] Each neighbor has a named relationship pattern and contract.
- [ ] Excluded and deferred items are named, with preconditions.
- [ ] Findings, decisions, and not-verified lists exist.
- [ ] Success-measure baselines recorded.
