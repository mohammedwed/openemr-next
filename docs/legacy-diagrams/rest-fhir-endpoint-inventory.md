# REST and FHIR endpoint inventory

This inventory is based on the route registries in
[`apis/routes/`](../apis/routes/) and the controllers under
[`src/RestControllers/`](../src/RestControllers/). The registries contain 97
standard routes, 5 portal routes, and 118 FHIR routes in this checkout.

`Service-owned` means that the controller delegates to a service and the
controller itself does not expose the backing SQL table. It is deliberately not
treated as proof of a specific table. `openemr_auth` means the controller
declares the authenticated OpenEMR API security scheme or is in the standard
authenticated route family. FHIR permissions are dynamically derived from
SMART scopes by [`ScopePermissionParser.php`](../src/RestControllers/SMART/ScopePermissionParser.php).

## Scope model

| Route family | Required scope/authorization |
|---|---|
| Standard `/api/*` | Usually `openemr_auth`; most controllers do not declare a resource-specific OAuth scope in the inspected layer. |
| Portal `/portal/*` | Patient-portal route family; the registry identifies these as patient-role routes. |
| FHIR `/fhir/*` | SMART `patient/Resource.crud`, `user/Resource.crud`, or `system/Resource.crud` as applicable; operation scopes are used for operations such as `$export` and `$docref`. |
| SMART configuration | Authorization is skipped for the SMART configuration endpoint in [`AuthorizationListener.php`](../src/RestControllers/Subscriber/AuthorizationListener.php). |
| Employer | Explicit `user/employer.read` or `patient/employer.read` in [`EmployerRestController.php`](../src/RestControllers/EmployerRestController.php). |

The route/controller declarations do not expose a resource-specific legacy
`AclMain` check for most standard endpoints. Therefore this document does not
invent one. Where only `openemr_auth` or dynamic SMART authorization is
confirmed, the more specific internal ACL is **not found in the inspected
route/controller layer**.

## Identity and access

| HTTP verb | Route | Controller/service | Tables touched | Required scope |
|---|---|---|---|---|
| GET | `/fhir/.well-known/smart-configuration` | [`SMARTConfigurationController.php`](../src/RestControllers/SMART/SMARTConfigurationController.php) | None visible | Public; authorization skipped |
| GET | `/fhir/metadata` | FHIR metadata route/controller | None visible | Auth behavior not separately declared in route registry |
| GET | `/fhir/OperationDefinition` | [`FhirOperationDefinitionRestController.php`](../src/RestControllers/FHIR/Operations/FhirOperationDefinitionRestController.php) | None visible | `openemr_auth` |
| GET | `/fhir/OperationDefinition/:operation` | Same | None visible | `openemr_auth` |
| GET | `/fhir/$export` | [`FhirOperationExportRestController.php`](../src/RestControllers/FHIR/Operations/FhirOperationExportRestController.php) | `categories` directly queried; export services own additional tables | SMART export scope |
| GET | `/fhir/Patient/$export` | Same | Export-service-owned | SMART patient export scope |
| GET | `/fhir/Group/:id/$export` | Same | Export-service-owned | SMART group/system export scope |
| GET | `/fhir/$bulkdata-status` | Same | Service-owned | Dynamic export-operation authorization |
| DELETE | `/fhir/$bulkdata-status` | Same | Service-owned | Dynamic export-operation authorization |
| GET | `/api/user` | [`UserRestController.php`](../src/RestControllers/UserRestController.php) → `UserService` | Service-owned | `openemr_auth`; specific ACL not confirmed |
| GET | `/api/user/:uuid` | Same | Service-owned | `openemr_auth`; specific ACL not confirmed |
| GET | `/api/version` | [`VersionRestController.php`](../src/RestControllers/VersionRestController.php) | None visible | `openemr_auth` |
| GET | `/api/background_service` | [`BackgroundServiceRestController.php`](../src/RestControllers/BackgroundServiceRestController.php) | `background_services` service-owned | `openemr_auth` |
| GET | `/api/background_service/:name` | Same | `background_services` service-owned | `openemr_auth` |
| POST | `/api/background_service/:name/run` | Same → background runner | Service-owned | `openemr_auth`; privileged ACL not confirmed |
| POST | `/api/background_service/$run` | Same | Service-owned | `openemr_auth`; implementation detail not exposed in registry |

OAuth/token controllers directly expose SQL access to `oauth_clients`, `users`,
and `patient_data` in [`AuthorizationController.php`](../src/RestControllers/AuthorizationController.php)
and to `oauth_clients` in
[`TokenIntrospectionRestController.php`](../src/RestControllers/TokenIntrospectionRestController.php).

## Patient registry and identity resources

| HTTP verb | Route/resource | Controller/service | Tables touched | Required scope |
|---|---|---|---|---|
| GET | `/api/patient` | [`PatientRestController.php`](../src/RestControllers/PatientRestController.php) → `PatientService` | Service-owned | `openemr_auth` |
| POST | `/api/patient` | Same | Service-owned | `openemr_auth` |
| GET | `/api/patient/:puuid` | Same | Service-owned | `openemr_auth` |
| PUT | `/api/patient/:puuid` | Same | Service-owned | `openemr_auth` |
| GET | `/fhir/Patient`, `/fhir/Patient/:uuid` | [`FhirPatientRestController.php`](../src/RestControllers/FHIR/FhirPatientRestController.php) | Service-owned | SMART Patient read scope |
| POST | `/fhir/Patient` | Same | Service-owned | SMART Patient create scope |
| PUT | `/fhir/Patient/:uuid` | Same | Service-owned | SMART Patient update scope |
| GET/POST/PUT | `/fhir/Person`, `/fhir/Person/:uuid` | [`FhirPersonRestController.php`](../src/RestControllers/FHIR/FhirPersonRestController.php) | Service-owned | SMART Person scopes |
| GET/POST/PUT | `/fhir/RelatedPerson`, `/fhir/RelatedPerson/:uuid` | [`FhirRelatedPersonRestController.php`](../src/RestControllers/FHIR/FhirRelatedPersonRestController.php) | Service-owned | SMART RelatedPerson scopes |
| GET | `/fhir/Group`, `/fhir/Group/:uuid` | [`FhirGroupRestController.php`](../src/RestControllers/FHIR/FhirGroupRestController.php) | Service-owned | SMART Group read scope |
| POST/PUT | `/fhir/Group`, `/fhir/Group/:uuid` | Route registry → `fhirWriteNotImplemented` | None | Scope is declared dynamically, but write is not implemented |

Candidate context: **patient registry**, with identity/access and therapy/group
relationships for Person, RelatedPerson, and Group.

## Scheduling

| HTTP verb | Route/resource | Controller/service | Tables touched | Required scope |
|---|---|---|---|---|
| GET | `/api/appointment` | [`AppointmentRestController.php`](../src/RestControllers/AppointmentRestController.php) → `AppointmentService` | Service-owned; service joins appointments to patient/provider/facility data | `openemr_auth` |
| GET | `/api/appointment/:eid` | Same | Service-owned | `openemr_auth` |
| GET | `/api/patient/:pid/appointment` | Same | Service-owned | `openemr_auth` |
| POST | `/api/patient/:pid/appointment` | Same | Service-owned | `openemr_auth` |
| GET | `/api/patient/:pid/appointment/:eid` | Same | Service-owned | `openemr_auth` |
| DELETE | `/api/patient/:pid/appointment/:eid` | Same | Service-owned | `openemr_auth` |
| GET | `/portal/patient/appointment` | Same | Service-owned | Patient portal role |
| GET | `/portal/patient/appointment/:auuid` | Same | Service-owned | Patient portal role |
| GET | `/fhir/Appointment`, `/fhir/Appointment/:uuid` | [`FhirAppointmentRestController.php`](../src/RestControllers/FHIR/FhirAppointmentRestController.php) | Service-owned | SMART Appointment read scope |
| POST | `/fhir/Appointment` | Same | Service-owned | SMART Appointment create scope |

Candidate context: **scheduling**. Related contexts are patient registry,
identity/access, facility/reference data, and clinical encounters.

## Clinical care

| HTTP verb | Route/resource | Controller/service | Tables touched | Required scope |
|---|---|---|---|---|
| GET/POST | `/api/patient/:puuid/encounter` | [`EncounterRestController.php`](../src/RestControllers/EncounterRestController.php) → `EncounterService` | Service-owned | `openemr_auth` |
| GET/PUT | `/api/patient/:puuid/encounter/:euuid` | Same | Service-owned | `openemr_auth` |
| GET | `/api/patient/:pid/encounter/:eid/vital`, `/:vid` | Same | Service-owned | `openemr_auth` |
| POST/PUT | `/api/patient/:pid/encounter/:eid/vital`, `/:vid` | Same | Service-owned | `openemr_auth` |
| GET | `/api/patient/:pid/encounter/:eid/soap_note`, `/:sid` | Same | Service-owned | `openemr_auth` |
| POST/PUT | `/api/patient/:pid/encounter/:eid/soap_note`, `/:sid` | Same | Service-owned | `openemr_auth` |
| GET | `/portal/patient/encounter`, `/portal/patient/encounter/:euuid` | Same | Service-owned | Patient portal role |
| GET/POST/PUT | `/fhir/AllergyIntolerance`, `/:uuid` | [`FhirAllergyIntoleranceRestController.php`](../src/RestControllers/FHIR/FhirAllergyIntoleranceRestController.php) | Service-owned | SMART AllergyIntolerance scopes |
| GET/POST/PUT | `/fhir/CarePlan`, `/:uuid` | [`FhirCarePlanRestController.php`](../src/RestControllers/FHIR/FhirCarePlanRestController.php) | Service-owned | SMART CarePlan scopes |
| GET/POST/PUT | `/fhir/CareTeam`, `/:uuid` | [`FhirCareTeamRestController.php`](../src/RestControllers/FHIR/FhirCareTeamRestController.php) | Service-owned | SMART CareTeam scopes |
| GET/POST/PUT | `/fhir/Condition`, `/:uuid` | [`FhirConditionRestController.php`](../src/RestControllers/FHIR/FhirConditionRestController.php) | Service-owned | SMART Condition scopes |
| GET/POST/PUT | `/fhir/Encounter`, `/:uuid` | [`FhirEncounterRestController.php`](../src/RestControllers/FHIR/FhirEncounterRestController.php) | Service-owned | SMART Encounter scopes |
| GET/POST/PUT | `/fhir/Goal`, `/:uuid` | [`FhirGoalRestController.php`](../src/RestControllers/FHIR/FhirGoalRestController.php) | Service-owned | SMART Goal scopes |
| GET/POST/PUT | `/fhir/Immunization`, `/:uuid` | [`FhirImmunizationRestController.php`](../src/RestControllers/FHIR/FhirImmunizationRestController.php) | Service-owned | SMART Immunization scopes |
| GET/POST/PUT | `/fhir/Observation`, `/:uuid` | [`FhirObservationRestController.php`](../src/RestControllers/FHIR/FhirObservationRestController.php) | Service-owned | SMART Observation scopes |
| GET | `/fhir/Procedure`, `/:uuid` | [`FhirProcedureRestController.php`](../src/RestControllers/FHIR/FhirProcedureRestController.php) | Service-owned | SMART Procedure read scope |
| POST/PUT | `/fhir/Procedure`, `/:uuid` | Route registry → `fhirWriteNotImplemented` | None | Write scope declared dynamically; not implemented |
| GET | `/fhir/Provenance`, `/:uuid` | [`FhirProvenanceRestController.php`](../src/RestControllers/FHIR/FhirProvenanceRestController.php) | Service-owned | SMART Provenance read scope |
| POST/PUT | `/fhir/Provenance`, `/:uuid` | Route registry → `fhirWriteNotImplemented` | None | Write scope declared dynamically; not implemented |
| GET/POST/PUT | `/fhir/Questionnaire`, `/:uuid` | [`FhirQuestionnaireRestController.php`](../src/RestControllers/FHIR/FhirQuestionnaireRestController.php) | Service-owned | SMART Questionnaire scopes |
| GET/POST/PUT | `/fhir/QuestionnaireResponse`, `/:uuid` | [`FhirQuestionnaireResponseRestController.php`](../src/RestControllers/FHIR/FhirQuestionnaireResponseRestController.php) | Service-owned | SMART QuestionnaireResponse scopes |

## Medications

| HTTP verb | Route/resource | Controller/service | Tables touched | Required scope |
|---|---|---|---|---|
| GET/POST/PUT/DELETE | `/api/patient/:pid/medication`, `/:mid` | [`ListRestController.php`](../src/RestControllers/ListRestController.php) → `ListService` | Service-owned | `openemr_auth` |
| GET | `/api/drug`, `/api/drug/:uuid` | [`DrugRestController.php`](../src/RestControllers/DrugRestController.php) → `DrugService` | Service-owned | `openemr_auth` |
| GET | `/api/prescription`, `/api/prescription/:uuid` | [`PrescriptionRestController.php`](../src/RestControllers/PrescriptionRestController.php) → `PrescriptionService` | Service-owned | `openemr_auth` |
| POST | `/api/prescription` | Same | Service-owned | `openemr_auth` |
| DELETE | `/api/prescription/:uuid` | Same | Service-owned | `openemr_auth` |
| GET/POST/PUT | `/fhir/Medication`, `/:uuid` | [`FhirMedicationRestController.php`](../src/RestControllers/FHIR/FhirMedicationRestController.php) | Service-owned | SMART Medication scopes |
| GET/POST/PUT | `/fhir/MedicationRequest`, `/:uuid` | [`FhirMedicationRequestRestController.php`](../src/RestControllers/FHIR/FhirMedicationRequestRestController.php) | Service-owned | SMART MedicationRequest scopes |
| GET | `/fhir/MedicationDispense`, `/:uuid` | [`FhirMedicationDispenseRestController.php`](../src/RestControllers/FHIR/FhirMedicationDispenseRestController.php) | Service-owned | SMART MedicationDispense read scope |
| POST/PUT | `/fhir/MedicationDispense`, `/:uuid` | Route registry → `fhirWriteNotImplemented` | None | Write scope declared dynamically; not implemented |

## Orders and results

| HTTP verb | Route/resource | Controller/service | Tables touched | Required scope |
|---|---|---|---|---|
| GET/POST/PUT | `/fhir/ServiceRequest`, `/:uuid` | [`FhirServiceRequestRestController.php`](../src/RestControllers/FHIR/FhirServiceRequestRestController.php) | Service-owned | SMART ServiceRequest scopes |
| GET | `/fhir/DiagnosticReport`, `/:uuid` | [`FhirDiagnosticReportRestController.php`](../src/RestControllers/FHIR/FhirDiagnosticReportRestController.php) | Service-owned | SMART DiagnosticReport read scope |
| POST/PUT | `/fhir/DiagnosticReport`, `/:uuid` | Route registry → `fhirWriteNotImplemented` | None | Write scope declared dynamically; not implemented |
| GET | `/fhir/Specimen`, `/:uuid` | [`FhirSpecimenRestController.php`](../src/RestControllers/FHIR/FhirSpecimenRestController.php) | Service-owned | SMART Specimen read scope |
| GET/POST/PUT | `/fhir/Observation`, `/:uuid` | [`FhirObservationRestController.php`](../src/RestControllers/FHIR/FhirObservationRestController.php) | Service-owned | SMART Observation scopes |
| GET | `/fhir/Procedure`, `/:uuid` | [`FhirProcedureRestController.php`](../src/RestControllers/FHIR/FhirProcedureRestController.php) | Service-owned | SMART Procedure read scope |

## Billing and insurance

| HTTP verb | Route/resource | Controller/service | Tables touched | Required scope |
|---|---|---|---|---|
| GET | `/api/patient/:puuid/insurance`, `/:uuid` | [`InsuranceRestController.php`](../src/RestControllers/InsuranceRestController.php) → `InsuranceService` | Service-owned | `openemr_auth` |
| POST | `/api/patient/:puuid/insurance` | Same | Service-owned | `openemr_auth` |
| PUT | `/api/patient/:puuid/insurance/:insuranceUuid` | Same | Service-owned | `openemr_auth` |
| GET | `/api/patient/:puuid/insurance/$swap-insurance` | Same | Service-owned | `openemr_auth` |
| GET | `/api/insurance_company`, `/:iid` | [`InsuranceCompanyRestController.php`](../src/RestControllers/InsuranceCompanyRestController.php) → `InsuranceCompanyService` | Service-owned | `openemr_auth` |
| POST/PUT | `/api/insurance_company`, `/:iid` | Same | Service-owned | `openemr_auth` |
| GET | `/api/insurance_type` | Same | Service-owned | `openemr_auth` |
| GET/POST | `/api/patient/:pid/transaction` | [`TransactionRestController.php`](../src/RestControllers/TransactionRestController.php) | Not visible in controller evidence | `openemr_auth` |
| PUT | `/api/transaction/:tid` | Same | Not visible in controller evidence | `openemr_auth` |
| GET/POST/PUT | `/fhir/Coverage`, `/:uuid` | [`FhirCoverageRestController.php`](../src/RestControllers/FHIR/FhirCoverageRestController.php) | Service-owned | SMART Coverage scopes |
| GET | `/api/patient/:puuid/employer` | [`EmployerRestController.php`](../src/RestControllers/EmployerRestController.php) → `EmployerService` | Service-owned | `user/employer.read` or `patient/employer.read` |

The route/controller layer does not confirm a direct `billing` table for these
resources; the backing tables are service-owned or not visible here.

## Documents

| HTTP verb | Route/resource | Controller/service | Tables touched | Required scope |
|---|---|---|---|---|
| POST | `/api/patient/:pid/document` | [`DocumentRestController.php`](../src/RestControllers/DocumentRestController.php) → `DocumentService` | Service-owned | `openemr_auth` |
| GET | `/api/patient/:pid/document`, `/:did` | Same | Service-owned | `openemr_auth` |
| GET | `/fhir/DocumentReference`, `/:uuid` | [`FhirDocumentReferenceRestController.php`](../src/RestControllers/FHIR/FhirDocumentReferenceRestController.php) | Service-owned | SMART DocumentReference read scope |
| POST | `/fhir/DocumentReference/$docref` | [`FhirDocumentRestController.php`](../src/RestControllers/FHIR/FhirDocumentRestController.php) | Service-owned | SMART DocumentReference operation scope |
| GET | `/fhir/Binary/:id` | FHIR document controller | Service-owned | SMART Binary read scope |
| GET | `/fhir/Media`, `/:uuid` | [`FhirMediaRestController.php`](../src/RestControllers/FHIR/FhirMediaRestController.php) | Service-owned | SMART Media read scope |
| POST/PUT | ordinary `/fhir/DocumentReference` routes | Route registry → `fhirWriteNotImplemented` | None | Write scope declared dynamically; not implemented |

## Messaging and portal

| HTTP verb | Route | Controller/service | Tables touched | Required scope |
|---|---|---|---|---|
| POST | `/api/patient/:pid/message` | [`MessageRestController.php`](../src/RestControllers/MessageRestController.php) → `MessageService` | Service-owned | `openemr_auth` |
| PUT | `/api/patient/:pid/message/:mid` | Same | Service-owned | `openemr_auth` |
| DELETE | `/api/patient/:pid/message/:mid` | Same | Service-owned | `openemr_auth` |
| GET | `/portal/patient` | [`PatientRestController.php`](../src/RestControllers/PatientRestController.php) | Service-owned | Patient portal role |
| GET | `/portal/patient/encounter`, `/:euuid` | [`EncounterRestController.php`](../src/RestControllers/EncounterRestController.php) | Service-owned | Patient portal role |
| GET | `/portal/patient/appointment`, `/:auuid` | [`AppointmentRestController.php`](../src/RestControllers/AppointmentRestController.php) | Service-owned | Patient portal role |

No messaging FHIR resource route was found in the inspected FHIR registry.

## Reference data and administration

| HTTP verb | Route/resource | Controller/service | Tables touched | Required scope |
|---|---|---|---|---|
| GET | `/api/facility`, `/api/facility/:fuuid` | [`FacilityRestController.php`](../src/RestControllers/FacilityRestController.php) → `FacilityService` | Service-owned | `openemr_auth` |
| POST/PUT | `/api/facility`, `/api/facility/:fuuid` | Same | Service-owned | `openemr_auth` |
| GET | `/api/list/:list_name` | [`ListRestController.php`](../src/RestControllers/ListRestController.php) → `ListService` | Service-owned | `openemr_auth` |
| GET | `/api/product` | [`ProductRegistrationRestController.php`](../src/RestControllers/ProductRegistrationRestController.php) | Not visible | `openemr_auth` |
| GET | `/api/immunization`, `/:uuid` | [`ImmunizationRestController.php`](../src/RestControllers/ImmunizationRestController.php) → `ImmunizationService` | Service-owned | `openemr_auth` |
| GET | `/api/procedure`, `/:uuid` | [`ProcedureRestController.php`](../src/RestControllers/ProcedureRestController.php) → `ProcedureService` | Service-owned | `openemr_auth` |
| GET | `/fhir/Device`, `/:uuid` | [`FhirDeviceRestController.php`](../src/RestControllers/FHIR/FhirDeviceRestController.php) | Service-owned | SMART Device read scope |
| GET | `/fhir/Location`, `/:uuid` | [`FhirLocationRestController.php`](../src/RestControllers/FHIR/FhirLocationRestController.php) | Service-owned | SMART Location read scope |
| POST/PUT | `/fhir/Location`, `/:uuid` | Route registry → `fhirWriteNotImplemented` | None | Write scope declared dynamically; not implemented |
| GET/POST/PUT | `/fhir/Organization`, `/:uuid` | [`FhirOrganizationRestController.php`](../src/RestControllers/FHIR/FhirOrganizationRestController.php) | Service-owned | SMART Organization scopes |
| GET/POST/PUT | `/fhir/Practitioner`, `/:uuid` | [`FhirPractitionerRestController.php`](../src/RestControllers/FHIR/FhirPractitionerRestController.php) | Service-owned | SMART Practitioner scopes |
| GET/POST/PUT | `/fhir/PractitionerRole`, `/:uuid` | [`FhirPractitionerRoleRestController.php`](../src/RestControllers/FHIR/FhirPractitionerRoleRestController.php) | Service-owned | SMART PractitionerRole scopes |
| GET | `/fhir/ValueSet`, `/:uuid` | [`FhirValueSetRestController.php`](../src/RestControllers/FHIR/FhirValueSetRestController.php) | Service-owned | SMART ValueSet read scope |

## Additional standard clinical resources

| HTTP verb | Route/resource | Controller/service | Tables touched | Required scope |
|---|---|---|---|---|
| GET | `/api/medical_problem`, `/:muuid`, and patient-scoped variants | [`ConditionRestController.php`](../src/RestControllers/ConditionRestController.php) → `ConditionService` | Service-owned | `openemr_auth` |
| POST/PUT/DELETE | patient-scoped medical-problem routes | Same | Service-owned | `openemr_auth` |
| GET | `/api/allergy`, `/:auuid`, and patient-scoped variants | [`AllergyIntoleranceRestController.php`](../src/RestControllers/AllergyIntoleranceRestController.php) → `AllergyIntoleranceService` | Service-owned | `openemr_auth` |
| POST/PUT/DELETE | patient-scoped allergy routes | Same | Service-owned | `openemr_auth` |
| GET/POST/PUT/DELETE | `/api/patient/:pid/surgery`, `/:sid` | [`ListRestController.php`](../src/RestControllers/ListRestController.php) → `ListService` | Service-owned | `openemr_auth` |
| GET/POST/PUT/DELETE | `/api/patient/:pid/dental_issue`, `/:did` | Same | Service-owned | `openemr_auth` |

## Resource-to-context mapping

| Candidate context | Resources |
|---|---|
| Identity/access | User, Practitioner, PractitionerRole, Person, RelatedPerson, OAuth/token and SMART configuration/metadata |
| Patient registry | Patient, Group, RelatedPerson, employer |
| Scheduling | Appointment |
| Clinical care | Encounter, AllergyIntolerance, CarePlan, CareTeam, Condition, Goal, Immunization, Observation, Procedure, Questionnaire, QuestionnaireResponse |
| Medications | Medication, MedicationRequest, MedicationDispense, Drug, Prescription, patient medication |
| Orders/results | ServiceRequest, DiagnosticReport, Specimen, Observation, Procedure |
| Billing | Coverage, Insurance, InsuranceCompany, InsuranceType, Transaction, Employer |
| Documents | Document, DocumentReference, Binary, Media |
| Messaging/portal | Message and portal patient/encounter/appointment routes |
| Reference data | Facility, Location, Organization, Device, ValueSet, List, Product, Immunization/procedure lookups |

These are candidate-context mappings, not claims that each controller is a
separate bounded context. Several resources intentionally span contexts:
`Observation` is both clinical data and a result; `Coverage` bridges billing
and patient registry; `Appointment` bridges scheduling, patient, provider,
facility, and encounter data.

## Not found or not confirmed

- No standard REST `PATCH` routes were found in the inspected route registry.
- No FHIR `PATCH` routes were found.
- No ordinary FHIR resource `DELETE` routes were confirmed; the confirmed FHIR
  delete is `DELETE /fhir/$bulkdata-status`.
- No patient, facility, insurance-company, encounter, or document standard
  `DELETE` route was found in the route registry.
- FHIR write routes for Group, Location, DiagnosticReport, DocumentReference,
  MedicationDispense, Procedure, and Provenance are registered but route to
  `fhirWriteNotImplemented`; they are not functioning write endpoints.
- Most service backing tables cannot be confirmed from the controller layer
  because controllers delegate to services. A table should be attributed only
  after inspecting the named service.
