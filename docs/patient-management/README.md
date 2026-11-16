# Patient Management: DDD design set

The strategic and tactical Domain-Driven Design for the **Patient Management** bounded context
(`src/Services/PatientManagement`), written against the code as it stands and the end state it is meant
to reach.

- **Status:** Draft for review. Nothing here is accepted until its decisions (doc 07, section 6) are
  answered and the relevant ADRs are written.
- **Written:** 2026-11-16
- **Follows:** the artifact list in `docs/domain-driven-design.md` ("Required artifacts")

## Reading order

| # | Document | Answers | Read it if you want |
|---|---|---|---|
| 01 | [Bounded context](01-bounded-context.md) | What is this context for, who uses it, what does it own and not own? | The scope and the capability list |
| 02 | [Ubiquitous language](02-ubiquitous-language.md) | What do the words mean, in code and on the wire? | A glossary, and the places language and code disagree |
| 03 | [Context map](03-context-map.md) | Who does it talk to, who leads, what crosses each boundary? | Integration, the Gateway, legacy coupling |
| 04 | [Domain model](04-domain-model.md) | Which aggregates, what rules, what invariants? | The tactical model and the rule catalogue |
| 05 | [Workflows and events](05-workflows-and-events.md) | What commands, queries, and events exist, and how do they flow? | Use cases, the event catalogue, the outbox and relay |
| 06 | [Data ownership and legacy mapping](06-data-ownership-and-legacy-mapping.md) | Which tables, columns, grants, and clocks? | The anti-corruption layer in detail |
| 07 | [Gaps and evolution](07-gaps-and-evolution.md) | What is wrong, in what order do we fix it, what must you decide? | The plan and the decision register |
| 08 | [UML diagrams](08-uml-diagrams.md) | What does it look like? | 22 diagrams: use case, structure, class, sequence, state, activity, ER, event flow |

**In a hurry:** read doc 07 sections 1, 3, and 6.

## Conventions

- **Built** means the code and a test exist and match the description. **Target** means a proposal.
  **Deferred** means a deliberate wait on something external. **(new)** marks a rule that legacy does not
  have, which changes behavior and needs a decision.
- **Ids** are permanent: capabilities `C1`…, aggregates `A1`…, rules `PT-`, `IC-`, `AD-`, `AS-`, `CT-`,
  `RL-`, `EM-`, commands `CMD-`, queries `QRY-`, events `EVT-`, workflows `WF-`, findings by their
  document prefix (`F-`, `G-`, `M`, `L`), decisions `DEC-`.
- **Names:** the context is *Patient Management*. Use the terms in doc 02 and none of its prohibited
  usages.
- **Evidence:** claims about legacy cite a file or a schema table, and were checked against
  `/home/mo/repos/openemr` where noted. Doc 07 section 7 lists what was **not** verified.
- **Diagrams** are Mermaid and render on GitHub.

## How this relates to existing material

| Existing | Relationship |
|---|---|
| `docs/openemr-ddd-design.md` §3.2 | The original sketch. This set **supersedes it for this context**; doc 01 section 10 lists every difference. |
| `docs/domain-driven-design.md` | The method this set follows. |
| `docs/m4-domain-notes.md` | Implementation notes and legacy findings. **Source material** for this set; not replaced. |
| `docs/m4-full-legacy-parity-plan.md` | The parity plan. Docs 01 and 07 carry it forward. |
| `docs/patient-view-pending-decisions.md` | Mapped into the decision register (doc 07, end of section 6). |
| `docs/architecture/0049`–`0063` | The decisions already made. This set cites them and proposes new ones. |
| `docs/m2-schema-inventory.md` | The boundary evidence behind docs 01 and 03. |

## Top findings

1. **The patient API is reachable without signing in.** The Gateway attaches the shared secret to every
   request (doc 03, M1; doc 07, P0-1).
2. **Registration can report failure after it has committed**, and a retry registers the same person
   again (doc 05, F-1).
3. **Patient numbers are random and up to 19 digits**, and break legacy's own allocator (doc 06, G-2).
4. **Nothing is audited**: no record of changes or reads (doc 06, G-8).
5. **Only one event exists and nothing delivers it**, so the next service cannot consume it (doc 05,
   F-5 and F-6).

## Out of scope

Patient portal credentials and login (excluded by decision); clinical data (Clinical Care); billing
money, claims, and eligibility (Billing); appointments (Scheduling); payer and facility master data
(Organization and Practice).
