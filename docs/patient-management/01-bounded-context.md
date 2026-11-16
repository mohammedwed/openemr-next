# Patient Management: bounded context definition

- **Status:** Draft for review
- **Perspective:** Target design. Each capability is marked **Built**, **Partial**, or **Target** so the
  distance between the code today and the intended end state stays visible.
- **Part of:** the Patient Management design set (`README.md` in this folder)
- **Supersedes:** §3.2 of `docs/openemr-ddd-design.md` for this context. That blueprint section stays as
  the original high-level sketch; where the two disagree, this document is the current one, and the
  differences are listed in [Differences from the blueprint](#differences-from-the-blueprint).

## 1. Purpose

Patient Management is the **system of record for who a patient is and the administrative facts attached to
that person**: identity, demographics, how to reach them, who covers their care costs, and the standing
wishes they have recorded about their care.

It answers questions like:

- Is this person already registered, and under which identifiers?
- What is this patient's name, date of birth, and contact information?
- Which insurance policy is in force at each level (primary, secondary, tertiary) on a given day?
- Has the patient recorded an advance directive, and is the document on file?
- Might this new registration be a duplicate of an existing patient?

It deliberately does **not** answer what happened clinically (encounters, vitals, diagnoses). That is
Clinical Care. The line between the two is already decided and verified: `patient_data` belongs here;
`form_encounter`, `form_vitals`, and `forms` belong to Clinical Care, and the only coupling is a
read-only reference by patient ID (`docs/m2-schema-inventory.md`, ROADMAP M2 decision gate).

## 2. Strategic classification

| Aspect | Value |
|---|---|
| Domain type | **Core**, per the blueprint (§2). See the note below. |
| Context name | **Patient Management**. `docs/domain-driven-design.md` calls the same capability "Patient administration". Use *Patient Management* in code, docs, and conversation; treat "administration" as an accepted synonym. |
| Deployment unit | Separately deployed service, `OpenEmrNext.PatientManagement.Api`, behind the Gateway (ADR 0060). |
| Maturity | In progress (ROADMAP M4). Registration, duplicate scoring, insurance, advance directives, and the FHIR Patient API are built. |

> **Review point: is this really Core?** Most of this context is master-data management, which the DDD
> literature usually places in *Supporting*. Two parts are real differentiated logic: effective-dated
> insurance resolution and duplicate detection. The blueprint's *Core* label is kept because the whole
> platform depends on patient identity, and because the portfolio slice is built around this context.
> If the team would rather spend modeling effort proportionally, the honest split is: insurance and
> duplicate rules are Core-grade, demographics and contacts are Supporting. Decide at review; nothing
> else in this set depends on the answer.

## 3. Actors and stakeholders

| Actor | What they do here | Today |
|---|---|---|
| **Registrar / front desk** | Registers patients, corrects demographics, reviews duplicate warnings | Register: Built. Correct demographics: Target. |
| **Insurance / billing clerk** | Adds policies, ends coverage, swaps primary and secondary, checks what is in force | Built (add, swap, effective view). Ending or editing a policy: Target. |
| **Medical-records staff** | Records advance-directive status, files directive documents, merges confirmed duplicates | Status and documents: Built. Duplicate review queue and merge: Target. |
| **Clinician** | Looks up a patient, reads demographics, insurance, and directive status | Read paths: Built. |
| **Integrating system** | Reads or creates patients through FHIR (US Core) | Read, search, create, validate: Built. Update: Target. |
| **Other bounded contexts** | Clinical Care, Billing, Scheduling consume patient identity and coverage | Only `PatientRegistered` exists; see doc 05. |
| **Identity (AuthFacade) and Gateway** | Authenticate the caller and forward it here | Gateway shared secret: Built (ADR 0055). Real user identity forwarding: Target. |

Domain stakeholders are not yet interviewed or named. Capabilities below come from the legacy source and
the roadmap, not from workflow interviews, so Target items are proposals to validate, not commitments.

## 4. Capabilities

| # | Capability | Responsibility | Status |
|---|---|---|---|
| C1 | **Registration** | Create a patient with a stable identity and the minimum required demographics | **Built** |
| C2 | **Demographics maintenance** | Change name, date of birth, sex, email and other demographics after registration, with history | **Target** (no `Patient.Update`; deferred, `m4-domain-notes.md`) |
| C3 | **Identity and identifiers** | Keep `uuid` (identity), `pid` (internal key), and `pubpid` (public/chart number) distinct and consistent | **Built** for assignment; **Target** for reassigning `pubpid` |
| C4 | **Duplicate detection** | Score a registration against existing patients; flag likely duplicates. Advisory only, never blocks | **Partial** (score computed and stored; no review queue) |
| C5 | **Insurance coverage** | Hold up to three effective-dated levels per patient, resolve what is in force on a day, swap levels | **Built**; ending or editing a policy is **Target** |
| C6 | **Advance directives** | Record completion status and review, and file directive documents (living will, durable power of attorney, DNR) | **Built** |
| C7 | **Contact information** | Addresses and telephone numbers with type, use, priority, and active period | **Target** |
| C8 | **Related persons** | Emergency contact, guardian, next of kin, with who may receive information or make decisions | **Target** |
| C9 | **Employer** | Current and past employer, occupation, industry (needed for workers' compensation) | **Target** |
| C10 | **Name history** | Keep previous names | **Target** |
| C11 | **Communication preferences** | Patient consent to be contacted by mail, voice, SMS, email (HIPAA fields on `patient_data`) | **Target** |
| C12 | **Deceased status** | Record date of death and its effect on the chart | **Target** |
| C13 | **Patient merge** | Combine two confirmed-duplicate charts into one | **Deferred**: blocked until Clinical Care and other services that hold `pid` data exist (`m4-full-legacy-parity-plan.md`) |
| C14 | **FHIR Patient API** | Standards-based read, search, create, and validate of Patient (US Core 8.0.0) | **Built** (read, search, create, `$validate`); update is **Target** |
| C15 | **Patient search and listing** | Find a patient by name and other criteria | **Partial** (whole-list endpoint; server-side paging is a pending decision) |

## 5. What this context owns

- The patient's **identity and identifiers**: `uuid`, `pid`, `pubpid`.
- **Demographics** as the registrar understands them.
- **Insurance coverage records** for a patient, and the rule that resolves which policy is in force.
- **Advance-directive status and the directive documents** filed for a patient.
- **Duplicate score**: the number, and what the number means.
- Where built or planned, a patient's **contact information, related persons, employer, name history, and
  communication preferences**.
- The `PatientRegistered` event and the outbox that carries it (and the events planned in doc 05).

## 6. What this context explicitly does not own

| Concern | Owner | Why it matters here |
|---|---|---|
| Encounters, vitals, clinical forms, problems, allergies, medications, issues list | **Clinical Care** (M5) | The blueprint lists "Patient Issue" under this context. It should not be: it is clinical. Clinical Care references a patient by ID only. |
| Authentication, sessions, tokens, who the signed-in user is | **AuthFacade / Identity** | This service receives a caller; it does not establish one. |
| Authorization policy (may this user see this patient) | **Authorization** (not yet built) | Today the whole API trusts a shared gateway secret. |
| Insurance **companies** (the payers themselves) | **Organization & Practice** (legacy `insurance_companies`) | A policy references a company by ID. This context never edits a company. |
| Claims, eligibility checks, payments, copay and deductible tracking | **Billing & Claims** | The blueprint places "copay/deductible" on the policy. Only the coverage facts belong here; money does not. |
| Appointments and patient flow | **Scheduling** | Needs only patient identity. |
| Patient portal accounts and credentials | **Out of scope entirely** | A separate authentication system in legacy (`patient_access_onsite`); excluded by decision in `m4-full-legacy-parity-plan.md`. |
| Facility and user (staff) records | **Organization & Practice** / **Identity** | `providerID` and `created_by` are references to staff, not owned data. |
| Reference code lists (`list_options`) | **Organization & Practice** | Read only, to translate FHIR codes. |
| Scanned documents in general (lab reports, images) | A future documents capability | This context files **advance-directive documents only**. The blueprint's general `PatientDocument` aggregate is not part of this context. |

## 7. Core workflows

1. **Register a patient.** Registrar submits demographics, the system validates, assigns identity,
   computes a duplicate score, stores the patient, and records `PatientRegistered`. *(Built)*
2. **Correct demographics.** Registrar changes a field; the change is validated, history is kept, the
   last-updated time moves, and an update event is recorded. *(Target)*
3. **Add or end coverage.** Clerk adds a policy at a level with a start date; later ends it or replaces
   it. The system rejects two policies at the same level and start date. *(Add: Built. End: Target.)*
4. **Swap coverage levels.** Clerk promotes secondary to primary; both policies change level together or
   not at all. *(Built)*
5. **Look up effective coverage.** Anyone asks what is in force on a day. *(Built)*
6. **Record an advance directive.** Staff files the document and sets the completion status and review.
   *(Built)*
7. **Review a probable duplicate.** Staff sees flagged patients, decides *same* or *distinct*, and
   optionally merges. *(Score: Built. Review decision and merge: Target / Deferred.)*
8. **Exchange via FHIR.** An external system searches, reads, validates, or creates a Patient. *(Built;
   update is Target.)*

Commands, queries, and events for each are in doc 05.

## 8. Legacy tables

The context runs **inside** the legacy `openemr` database (ADR 0049) and is the only writer to the rows
it owns, because the legacy registration screen is retired. Table names below were checked against
`sql/database.sql` and the repository SQL, not taken from the blueprint.

| Table | Access today | Role |
|---|---|---|
| `patient_data` | Read, **write** | The patient. Also holds the advance-directive flags (`completed_ad`, `ad_reviewed`, `advance_directive_user_authenticator`), `dupscore`, HIPAA preference columns, `deceased_date`. |
| `insurance_data` | Read, **write** | Policies. `UNIQUE (pid, type, date)` makes it a history per level. |
| `documents` | Read, **write** (advance directives only) | Directive files. `id` is not auto-increment. |
| `categories_to_documents` | **Write** | Links a document to the "Advance Directive" category. |
| `sequences` | **Write** | Legacy's shared single-row ID counter, used to allocate `documents.id`. Shared with legacy writers, so this is a coupling to manage, not just data. |
| `patient_management_outbox` | **Write** | New table, same database, so event and business write commit atomically. The only table this service creates. |
| `categories` | Read | Finds the "Advance Directive" category by name. |
| `list_options` | Read | Translates race, ethnicity, language, and other coded values for FHIR. |
| `users` | Read | Resolves the patient's provider to a UUID for FHIR. |
| `insurance_companies` | Not yet read | Policies hold a company ID; validating that the company exists is Target. |
| `employer_data` | Target | Employer (C9). |
| `patient_history` | Target | Previous names (C10). |
| `contact`, `contact_address`, `contact_telecom`, `contact_relation`, `person`, `person_patient_link` | Target | Contacts and related persons (C7, C8). The schema models a related person as `contact` → `person` → `contact_relation`. The blueprint's mapping points at `DemographicsRelatedPersonsService`, which reads and writes a `patient_related_persons` table, but **no SQL file in the legacy checkout creates that table**, so treat that service as unverified until it is confirmed against a running install. Also note that a patient's own current address, phones, guardian, and mother's name are plain **columns on `patient_data`** (`street`, `phone_home`, `guardiansname`, and so on), a second place the same facts live. |
| `patient_reminders`, `dated_reminders`, `pnotes`, `issue_encounter`, `care_teams`, `history_data` | **Not owned** | Clinical or workflow data keyed by `pid`. Named so nobody assumes they belong here. See `m2-schema-inventory.md`. |

The full anti-corruption and ownership strategy is doc 06.

## 9. Security, audit, and continuity requirements

Everything here is **protected health information**. The requirements below are the obligations the
design must meet; their current state is in doc 07.

- **Authorization.** Every operation must be attributable to an authenticated user and allowed by policy.
  *Today:* a shared gateway secret gates the API (ADR 0055); the acting user's ID is **caller-supplied**
  (`RegisteredBy`, `OwnerId`, `AuthenticatorUserId`) and not verified. This is the largest open gap.
- **Audit.** Who registered, changed, or filed what, and when, must be recorded. *Today:*
  `created_by`/`registeredBy` is stored for registration; changes to demographics (Target) need history.
- **Data minimization.** Events and APIs carry only what a consumer needs. `PatientRegistered` already
  carries a curated subset, not the row. SSN must never appear in a summary response or event.
- **Integrity of legacy data.** Rows this service did not write (zero dates, free-text sex, directive
  flags not reconciled with documents) must be readable without failure.
- **Continuity.** A registration and its event commit together or not at all (built). A directive file
  and its row do not (compensating delete only; see doc 06).
- **Retention.** No hard deletes of clinical-administrative records without an explicit rule. Merge in
  legacy hard-deletes; this context must not copy that.

## 10. Differences from the blueprint

| Blueprint §3.2 | This design | Reason |
|---|---|---|
| `Patient` aggregate holds Contacts, Related Persons, Employer | Contacts, related persons, and employer are separate aggregates, referenced by patient identity | They change on their own schedule and for different reasons; folding them into one aggregate makes every contact edit contend with demographics and registration. Detail in doc 04. |
| `InsurancePolicy` is an aggregate root | The aggregate is the per-patient **`InsuranceCoverage`** cluster; `InsurancePolicy` is an entity inside it | Level swap is a two-policy operation with a cluster-wide rule (ADR 0053). |
| `PatientDocument` aggregate (any file) | **`AdvanceDirectiveDocument`** only | Scope was narrowed to what has a real invariant here; general document management is not part of this context. |
| "Patient Issue" in the language | Removed | Clinical; belongs to Clinical Care. |
| "Patient Portal Credentials" in the language | Removed | Excluded by decision. |
| Copay and deductible on the policy | Removed | Financial; belongs to Billing. |
| Events: six, including `PortalCredentialsIssued`, `DocumentUploaded` | Revised set in doc 05 | Portal is out; some events have no consumer yet. |
| Shared `PatientId` in a SharedKernel package | Open | Today the identity crossing the boundary is the `uuid`; `pid` is also used in routes and events. See doc 03. |

## 11. Open questions for this document

1. **Core versus Supporting** (section 2): which classification do we adopt?
2. **Is `pubpid` the medical record number?** Clinicians may expect an MRN. Pending decision 4 in
   `docs/patient-view-pending-decisions.md`; it decides whether doc 02 needs a separate MRN term.
3. **Are C7 to C12 in scope for the portfolio slice, or documented as the end state only?** This set
   describes them as the target either way; the answer affects ordering in doc 07.
4. **Who validates the Target capabilities?** No domain stakeholder has reviewed them; they come from
   legacy behavior.
