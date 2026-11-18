# Clinical Care: DDD design set

The strategic and tactical Domain-Driven Design for the **Clinical Care** bounded context (`src/Services/ClinicalCare`), written against the code as it stands on `main` (Nov 18, 2026), not against a plan.

- **Status:** Written from the code. Where a claim is about legacy OpenEMR it cites the file or table and was checked against the OpenEMR 8.0.0 source (`/home/mo/repos/openemr`) or the running container.
- **Companion:** [`docs/patient-management/`](../../patient-management/README.md) is the same kind of set for the other domain service, and this one deliberately uses the same document names.
- **Not here:** a gaps document. What is still open lives in [`ROADMAP.md`](../../../ROADMAP.md) ("What remains before the post-M5 gate") and, per feature, in the slice notes `docs/clinical-care/01` to `25`, which stay the record of *how each slice was researched and built*. This set says *what the context is*.

## Reading order

| # | Document | Answers | Read it if you want |
|---|---|---|---|
| 01 | [Bounded context](01-bounded-context.md) | What is this context for, who uses it, what does it own and not own? | The scope and the capability list |
| 02 | [Ubiquitous language](02-ubiquitous-language.md) | What do the words mean, in code and on the wire? | A glossary, and the places language and code disagree |
| 03 | [Context map](03-context-map.md) | Who does it talk to, who leads, what crosses each boundary? | Integration, the gateway, legacy coupling |
| 04 | [Domain model](04-domain-model.md) | Which aggregates, which rules, which invariants, and what keeps them true? | The tactical model and the rule catalogue |
| 05 | [Workflows and events](05-workflows-and-events.md) | What commands, queries and events exist, and how does a request flow? | Use cases, the request pipeline, the event catalogue |
| 06 | [Data ownership and legacy mapping](06-data-ownership-and-legacy-mapping.md) | Which tables, columns, grants, tokens and clocks? | The anti-corruption layer in detail |
| 07 | [UML diagrams](07-uml-diagrams.md) | What does it look like? | 19 diagrams: use case, structure, class, sequence, state, activity, ER |

**In a hurry:** read 01, then the "What keeps it true" table in 04 and the pipeline in 05.

## Conventions

- **Built** means the code and a test exist and match the description. Nothing in this set is a proposal.
- **Ids** are the ones the code and tests already use, so a rule can be found by searching for it: `CC-` (encounter), `CV-` (vitals), `DOM-` (domain-wide rules), `EVT-` (events), `SEC-` (security), `LEG-` (legacy coexistence). New ids in this set are `CMD-`, `QRY-`, `WF-`, and the labels `CV-1` to `CV-3` and `IS-1` to `IS-5` in doc 04, which name rules the code states without an id (search for their message text).
- **Names:** the context is *Clinical Care*. Use the terms in 02 and none of its prohibited usages.
- **Diagrams** are Mermaid and render on GitHub.
- **Two version kinds.** A write is conditional on the version the caller read. Rows with a usable change stamp use it (`ConcurrencyToken`, an `ETag` like `"1a2b3c"`); rows without one use a content hash (`ContentVersion`, an `ETag` like `"c-…"`). Doc 06 says which table uses which.

## The shape in one paragraph

Clinical Care is the second extracted domain service. It records and corrects the clinical facts of a visit (encounters, vitals, problems, allergies, immunizations, SOAP notes, clinical notes) **by writing OpenEMR's own tables through a least-privilege database user**, so the legacy screens keep working against the same rows. It never copies a patient's demographics: it holds a reference (`uuid`, `pid`) learned from Patient Management's `PatientRegistered` event. Every write is conditional on the version the caller read, is performed as the signed-in user the gateway verified, and lands together with an audit row and an outbox event in one transaction. It also serves read-only FHIR R4 for five resources.
