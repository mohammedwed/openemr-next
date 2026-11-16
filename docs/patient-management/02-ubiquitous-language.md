# Patient Management: ubiquitous language

- **Status:** Draft for review
- **Part of:** the Patient Management design set (`README.md` in this folder)
- **Scope:** terms whose meaning is fixed **inside** the Patient Management context. The same word may
  mean something different elsewhere; section 7 lists the collisions.

Each entry gives: definition, accepted synonyms, what **not** to use it for, the legacy equivalent, how it
appears in code, and how it appears on the wire (API or event). **Built** terms exist in code today;
**Target** terms belong to capabilities in doc 01 that are not built yet, and their code names are
proposals.

## 1. Rules for using this language

1. One term, one meaning, in conversation, docs, code, API fields, and event names.
2. A legacy column name is **not** a domain term. `sex`, `date`, `provider`, and `type` are storage
   names; use the terms below and let the anti-corruption layer translate (doc 06).
3. Where code and language disagree today, the disagreement is listed in section 8 and is a defect in
   one of the two, not a second accepted meaning.
4. A new term is added here in the same change that introduces it in code.

## 2. Identity and identifiers

| Term | Definition | Not to be used for | Legacy | Code | Wire |
|---|---|---|---|---|---|
| **Patient** | A person registered to receive care, identified by a stable identity. The aggregate root of the context. **Built** | A person who merely has an appointment, a guarantor, or a related person. A related person who *becomes* a patient is a new registration linked to the person (see **Person link**). | `patient_data` row | `Patient` | `/patients`, FHIR `Patient` |
| **Patient identity** (`Uuid`) | The one value that identifies a patient across systems and over time. Never reassigned, never reused. | Anything staff type or read aloud. | `patient_data.uuid`, the only `UNIQUE` identifier legacy's own FHIR API exposes as `Patient.id` | `Patient.Uuid` (`Guid`) | `uuid`, FHIR `Patient.id`, `PatientRegistered.Uuid` |
| **Patient number** (`Pid`) | The internal numeric key other tables and routes use to refer to a patient. Unique, opaque, not sequential. Treated as a **reference**, not as identity. | Display to staff, a count of patients, or a sort key. | `patient_data.pid` (what `form_encounter.pid` and others join on) | `Patient.Pid` (`long`) | `pid` in insurance and directive routes, `PatientRegistered.Pid`, `PatientSummaryResponse.Pid` |
| **Chart number** (`Pubpid`) | The identifier staff see and say. Defaults to the patient number if not supplied. A business identifier **attached to** the patient, not the patient's identity. | Identity, or a lookup key in a stored reference. | `patient_data.pubpid` | `Patient.Pubpid` (`string`) | `pubpid`, FHIR `identifier` |
| **Row id** | The database row number. Carries no uniqueness guarantee in legacy. | Anything. It is an implementation detail and must not leave Infrastructure. | `patient_data.id` | `Patient.Id` (set by EF after insert) | never exposed |
| **Medical record number (MRN)** | Not yet a term. Whether the *chart number* **is** the MRN is undecided (doc 01 question 2). Until decided, do not use "MRN" in code or contracts. | | none separate | none | none |

Why three identifiers: `docs/m4-domain-notes.md` ("Patient identifier glossary") settles it. Legacy's own
FHIR API already treats `uuid` as identity and `pubpid` as a business identifier; this context follows the
same split.

## 3. Registration and demographics

| Term | Definition | Not to be used for | Legacy | Code | Wire |
|---|---|---|---|---|---|
| **Registration** | The act of creating a patient. One command, one outcome: a new patient and a `PatientRegistered` event, or nothing. **Built** | Editing an existing patient, or creating an encounter. | `PatientService::insert` | `RegisterPatientUseCase`, `Patient.Register` | `POST /patients`, `POST /fhir/Patient` |
| **Registration** (value) | The validated set of fields supplied to register: first name, last name, sex, date of birth, optional email and trusted email. Cannot exist in an invalid state. | The saved patient. | `PatientValidator` rule set | `PatientRegistration.Create` | `RegisterPatientRequest` |
| **Registered by** | The staff user who performed the registration. Required, positive, **currently caller-supplied and unverified**. | The patient's provider, or the patient. | `patient_data.created_by` | `Patient.RegisteredBy` | `registeredBy` |
| **Registered at** | The moment of registration (UTC). | The last time the patient changed. | `patient_data.regdate` | `Patient.RegisteredAtUtc` | `PatientRegistered.RegisteredAtUtc` |
| **Last updated** | The moment the patient last changed. Set at registration, moved by each later change. | The registration time. | `patient_data.date`, **a naming trap**: a column called `date` is the last-modified time | private `_date`; exposed as FHIR `meta.lastUpdated` | FHIR `meta.lastUpdated` |
| **Sex** | The free-text value staff record in the field the legacy UI labels "Gender" (4 to 30 characters). Kept under the storage column's name because no closed set exists to rename it to. | Gender identity, administrative sex, or a closed enum. See section 8. | `patient_data.sex` | `Patient.Sex` | `sex` |
| **Administrative sex** | The coded value (US Core extension) derived from sex for exchange. Always present on a FHIR record, defaulting to *unknown*. | Sex as typed. | `list_options.codes` (`SYSTEM:CODE`) | `PatientFhirView` coded field | FHIR `us-core-sex` extension |
| **Gender identity** | How the patient identifies. Coded; **omitted** from a FHIR record when blank (unlike administrative sex, which defaults). | Sex. | `patient_data.gender_identity` | `PatientFhirView` coded field | FHIR extension |
| **Race**, **Ethnicity** | Coded OMB categories. Race defaults to *unknown*; ethnicity is **omitted** when blank. These two behaviors differ on purpose, matching legacy. | Free text. | `patient_data.race`, `.ethnicity` | `ResolvedCode`, `EthnicityCategory` | FHIR US Core extensions |
| **Date of birth** | The patient's birth date. Required. | A partial date. Legacy tolerates partial or zero dates in old rows; this context never writes one. | `patient_data.DOB` | `Patient.DateOfBirth` (`DateOnly`) | `dateOfBirth`, FHIR `birthDate` |
| **Email**, **Trusted email** | Contact email; and the "Direct" secure-messaging address. Both optional, validated if present. | Each other. | `patient_data.email`, `.email_direct` | `Patient.Email`, `.TrustedEmail` (null when blank) | `email`, `trustedEmail` |
| **Resolved code** | A code from a named vocabulary (system, value, optional display), already looked up. Never blank in system or value. | A raw string from storage. | `list_options` row | `ResolvedCode` | FHIR `Coding` |
| **Patient record view** | The read-only, display-shaped patient: demographics plus resolved codes. Built for reading and FHIR output, not for changing. | The `Patient` aggregate. | none | `PatientFhirView` | `PatientSummaryResponse`, FHIR `Patient` |

## 4. Duplicate detection

| Term | Definition | Not to be used for | Legacy | Code | Wire |
|---|---|---|---|---|---|
| **Duplicate score** | A number rating how alike a patient is to the most alike other patient. Weighted: email, date of birth, name, and sex each 6 when they match exactly; sound-alike last name 2; shared phone 1; maximum 27. **Advisory. Never blocks registration.** **Built** | A decision that two records are one person. | `patient_data.dupscore`, `DuplicatePatientService` | `ScorePatientForDuplicatesUseCase`, `IDuplicateScorer` | stored; level only in API where exposed |
| **Worth reviewing** | Score at or above 12: two strong identifiers agree. | Proof. | display threshold | `DuplicateScoreLevel.WorthReviewing`, `DuplicateScore.DisplayThreshold` | n/a |
| **Near certain** | Score at or above 17: three strong identifiers agree. | Proof, or a reason to auto-merge. | highlight threshold | `DuplicateScoreLevel.NearCertain`, `DuplicateScore.HighlightThreshold` | n/a |
| **Declared unique** | An operator has said this chart is not a duplicate; excluded from scoring from then on. Stored as `-1`. **Target** (value reserved, workflow absent) | A low score. | `dupscore = -1` | `DuplicateScore.Unique` | n/a |
| **Pending rescore** | Queued for recomputation. Stored as `-9`. **Target** | A score of zero. | `dupscore = -9` | `DuplicateScore.Pending` | n/a |
| **Duplicate review** | The human decision, on a flagged pair, of *same* or *distinct*. **Target** | The score. | admin duplicate report | none | none |
| **Merge** | Making one patient the surviving record and retiring the other, repointing everything that referred to it. **Deferred** (needs other services) | Deleting a patient. Legacy hard-deletes; this context must not. | patient merge tooling | none | none |

## 5. Insurance coverage

| Term | Definition | Not to be used for | Legacy | Code | Wire |
|---|---|---|---|---|---|
| **Coverage** | One patient's whole set of insurance policies. The consistency boundary for insurance. **Built** | A single policy, or what the payer will pay. | rows in `insurance_data` for one `pid` | `InsuranceCoverage` | n/a |
| **Insurance policy** | A patient's enrollment under a payer's plan, at a level, over a period. **Built** | The payer. The financial terms. | `insurance_data` row | `InsurancePolicy` | `AddInsurancePolicyRequest`, `InsurancePolicyAddedResponse` |
| **Level** | The order staff gave a policy: primary, secondary, tertiary. It **is** the order; the system does not compute coordination of benefits. | Priority computed from rules. | `insurance_data.type` | `InsuranceLevel` | `"primary"`, `"secondary"`, `"tertiary"` |
| **Start date**, **End date** | When the policy begins and ends. End is optional (open ended). Both inclusive. | Registration date. | `insurance_data.date`, `.date_end` | `StartDate`, `EndDate` | `startDate`, `endDate` |
| **Payer** (insurance company) | The organization providing coverage. **Referenced, not owned.** | A policy. | `insurance_companies` | `CompanyId` | `companyId` |
| **Policy number**, **Group number**, **Plan name** | Identifiers and label printed on the member's card. | Each other. | `policy_number`, `group_number`, `plan_name` | same | same |
| **Effective policy** | The policy actually in force at a level on a given day. Not necessarily the newest row. | The most recently added policy. | `EffectiveInsuranceRule::choose()` | `EffectiveInsurancePolicy`, `EffectiveInsuranceRule` | `EffectiveInsuranceResponse` |
| **In force** | A policy is in force on day D when its start is on or before D and its end is on or after D. A blank start means always; a blank end means open. | Active, enabled. | rule text in `EffectiveInsuranceRule.php` | rule in `EffectiveInsuranceRule` | n/a |
| **Unassigned** | A policy row with no payer, added by staff to **end** a level. Means *no coverage at this level on this day*. | A missing or invalid policy. | provider blank or `0` | `StoredId` returns null | level absent from the effective set |
| **Swap** | Moving a policy to another level; whichever policy holds that level takes the first one's old level, in one step. Never leaves two policies at one level and date. | Edit of a single policy. | `swapInsurance()` | `InsuranceCoverage.Swap`, `SwapInsuranceUseCase` | `POST …/insurance/swap` |
| **Sentinel date** | A far-future date (`9999-12-31`) used **only inside persistence** to park a policy while a swap would momentarily collide on the unique `(pid, type, date)` key. Not a domain concept. | A policy end date. | swap workaround | `EfInsurancePolicyRepository` | never exposed |
| **Zero date**, **partial date** | Legacy storage artifacts (`0000-00-00`, `2026-05-00`) found on old rows. Read tolerantly, never written. | Real dates. | permissive legacy `sql_mode` | handled in `EffectiveInsuranceRule` | n/a |

## 6. Advance directives

| Term | Definition | Not to be used for | Legacy | Code | Wire |
|---|---|---|---|---|---|
| **Advance directive** | A patient's recorded wishes about future care, in two independent parts: a **status** and zero or more **documents**. Legacy never reconciles the two; neither does this context. **Built** | A single record that is complete only when both parts agree. | status flags on `patient_data` plus `documents` | n/a (two models) | `/advance-directives/status`, `/documents` |
| **Directive status** | Whether staff have recorded that the patient has completed a directive: **yes**, **no**, or **unknown**. *Unknown* is a real state ("not asked or not recorded"), distinct from *no*. | A boolean. | `patient_data.completed_ad` (`'YES'`, `'NO'`, `''`) | `AdvanceDirectiveCompletionStatus` | `"yes"`, `"no"`, `"unknown"` |
| **Directive reviewed** | The moment a staff member validated the directive, and **who**. | When it was filed. | `ad_reviewed`, `advance_directive_user_authenticator` | repository fields | `SetAdvanceDirectiveStatusRequest.AuthenticatorUserId` |
| **Authenticator** | The staff user who reviewed and validated the directive. **Caller-supplied today.** | The patient, or the uploader. | `advance_directive_user_authenticator` | `AuthenticatorUserId` | `authenticatorUserId` |
| **Directive document** | A file the patient or staff filed as a directive. Immutable once filed. | Any other scanned document. | `documents` row in category "Advance Directive" | `AdvanceDirectiveDocument` | `AdvanceDirectiveDocumentResponse` |
| **Directive type** | Living will, durable power of attorney, or do-not-resuscitate order. | A free label. | none; legacy infers it from keywords in the **document name** | `AdvanceDirectiveType` | `"living_will"`, `"durable_power_attorney"`, `"dnr_order"` |
| **Content hash** | SHA3-512 of the file bytes, kept for integrity. | A security signature. | `documents.hash` | `Sha3Hash` | not exposed |
| **Drive id** | A second, separate UUID naming the file on disk. Not the document's identity. | The document's `Uuid`. | `documents.drive_uuid` | `DriveUuid` | not exposed |
| **Effective date** | When the directive takes effect, if known. | Upload date. | none dedicated | `EffectiveDate` | `effectiveDate` |
| **Owner** | The staff user who filed the document. **Caller-supplied today.** | The patient. | `documents.owner` | `OwnerId` | `ownerId` |

## 7. Events and integration

| Term | Definition | Code | Wire |
|---|---|---|---|
| **Domain event** | A fact that happened in this context, named in the past tense. | `PatientRegisteredEvent` today | outbox row |
| **PatientRegistered** | A patient was registered. Carries identity, patient number, first and last name, and time. **Not** the whole record. **Built** | `PatientRegisteredEvent.EventType` | `PatientRegistered` |
| **Outbox** | The table where an event is written in the same transaction as the change it records, so both happen or neither does. A relay (not built) delivers it onward. | `OutboxMessage` | `patient_management_outbox` |
| **Anti-corruption layer** | The translation between legacy storage and this model. All legacy names, dates, and quirks stop here. | `Infrastructure` | n/a |
| **Gateway secret** | The shared value proving a request came through the Gateway. **Not** a user identity. | `InternalGatewayOptions` | `X-Internal-Gateway-Secret` |

Further events are proposed in doc 05; they are not language yet.

## 8. Target terms (capabilities not built)

Proposed vocabulary for doc 01 capabilities C2 and C7 to C12. Names are provisional until validated by
domain stakeholders.

| Term | Proposed definition | Legacy source |
|---|---|---|
| **Demographic correction** | A change to registration fields after the fact, with history. | `PatientService::update` |
| **Previous name** | A name the patient formerly used, with the date it ended. | `patient_history` (`previous_name_*`) |
| **Contact point** | A telephone, email, or similar means of reaching someone, with use (home, work, mobile), rank, and active period. | `contact_telecom` |
| **Address** | A postal address with type, use, active period, and a reason when ended (moved, mail returned). | `contact_address`, `addresses` |
| **Related person** | A person connected to the patient, with a relationship. | `person`, `contact_relation` |
| **Emergency contact** | A related person to call in an emergency. | `contact_relation.is_emergency_contact` |
| **Medical decision maker** | A related person allowed to make medical decisions for the patient. | `can_make_medical_decisions` |
| **Information recipient** | A related person allowed to receive medical information. | `can_receive_medical_info` |
| **Person link** | The link recording that a person (such as a former related person) is also a patient. | `person_patient_link` |
| **Employer** | The patient's employer, occupation, industry, and employment period. | `employer_data` |
| **Communication preference** | Whether the patient allows contact by mail, voice, SMS, or email. | `patient_data.hipaa_*` |
| **Deceased** | The patient has died; date recorded. | `patient_data.deceased_date` |

## 9. Collisions with other contexts

| Word | Here | Elsewhere | Rule |
|---|---|---|---|
| **Patient** | The registered person and their administrative record | Clinical Care: the subject of an encounter | Clinical Care holds a **reference** (identity and patient number), not a copy of this model. |
| **Provider** | In insurance: the **payer** (legacy column `provider` holds the company id). In demographics: the patient's assigned clinician (`providerID`) | Clinical Care: the treating clinician | Never write "provider" unqualified. Use **payer** and **assigned provider**. |
| **Type** | Legacy column holding the insurance **level** | Directive type; document type | Never "type" alone. Use **level**, **directive type**. |
| **Date** | Legacy column meaning *start date* (insurance) or *last updated* (patient) | any | Never "date" alone. |
| **Status** | Directive status (yes, no, unknown) | Encounter status, claim status | Always qualify. |
| **User** | The authenticated staff member acting | AuthFacade: an identity that can authenticate | Here only as **registered by**, **owner**, **authenticator**: a reference to a staff user, never a new concept. |
| **Document** | A filed **directive document** only | Documents capability (any file) | Do not call a directive document simply "document" in contracts. |
| **Event** | A domain event | Calendar event (Scheduling) | Always "domain event" when unclear. |

## 10. Language and code disagree today

| # | Disagreement | Effect | Proposed resolution |
|---|---|---|---|
| L1 | **Sex** is free text (4 to 30 characters); US Core and the glossary treat *administrative sex* as coded | A user can register "Unspecified" or a typo; mapping to a code falls back to *unknown* silently | Decide: keep free text for parity, or constrain to a list. Pending decision. |
| L2 | `PatientRegisteredEvent` carries `Pid`, but the blueprint's shared kernel says identity is `uuid` | Consumers may key on the wrong value | Doc 03 decides what the cross-context reference is. |
| L3 | `Patient.Pid` is a random 63-bit number, not sequential | Unique in practice, enforced only by the unique key; no retry on the (very unlikely) collision | Doc 06 records this as a design risk; add a retry or switch allocation. |
| L4 | `AdvanceDirectiveType` is **inferred from a document's name** in legacy | A renamed or foreign document may classify wrongly | Keep for parity; do not let new code rely on the name. |
| L5 | **Registered by**, **owner**, **authenticator** are caller-supplied | The audit trail can name the wrong person (`patient-view-pending-decisions.md` item 1) | Resolve with identity forwarding; doc 07. |
| L6 | `Patient.Sex` and API field `sex`; legacy validator calls it "Gender" | Two names for one thing | Keep **sex** as the term; never write "gender" for this field. |
| L7 | Request DTOs say `RegisteredBy`; FHIR create takes it as a query parameter | Same concept, two shapes | Both are removed once identity is forwarded. |

## 11. Prohibited usage, summarized

- Do not use `id` for a patient outside Infrastructure.
- Do not use **MRN** until the chart-number question is answered.
- Do not say **provider** without **payer** or **assigned provider**.
- Do not say **type**, **date**, or **status** without a qualifier.
- Do not say **gender** for the `sex` field.
- Do not describe the duplicate score as a verdict, or merge as deletion.
- Do not call a directive document a "document" in an API contract.
- Do not carry portal, billing-money, or clinical-issue terms into this context.
