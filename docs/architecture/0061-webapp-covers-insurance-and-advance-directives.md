# 0061. WebApp's frontend extends to insurance and advance directives

- **Status:** Accepted
- **Date:** 2026-11-15
- **Builds on:** [0055](0055-patientmanagement-api-gated-by-a-shared-secret-identity-forwarding-deferred.md), [0060](0060-patientmanagement-is-deployed-and-reachable-through-the-gateway.md)

## Context

The first WebApp frontend slice (ADR 0060) only covered registration and a patient list. PatientManagement.Api
already had insurance (add policy, effective coverage, swap) and advance directives (upload document,
status, document list/download) fully built; nothing in WebApp called them. FHIR gets no UI here --
legacy OpenEMR has no human-facing screen for its own FHIR API either, it is an interop surface, not a
screen a user clicks through, so there is no legacy equivalent for a frontend to match.

**A real blocker, found before any UI could be built**: every insurance/advance-directive route is
keyed by `pid` (a legacy integer), but the patient list only ever exposed `Uuid`
(`PatientFhirView`/`PatientSummaryResponse` never carried `Pid` -- nothing needed it before now).
Fixed by adding `Pid` to both: it is already a column in the same query that already selects `uuid`
(`EfPatientFhirRepository.BaseSelect`), so this is one more column through an existing read path, not
new Domain logic. A new `GET /api/v1/patients/{uuid}` endpoint (reusing `GetPatientFhirRecordUseCase`,
already used by the FHIR `GET /fhir/Patient/{uuid}` endpoint) gives the detail page a way to resolve a
patient's `pid` from the `uuid` the URL carries.

**Three more real bugs, found only by exercising the real deployed stack, not by unit/integration
tests**, in order of discovery:

1. `DownloadAdvanceDirectiveDocumentContentAsync`'s generated client method returns a bodyless `Task`
   -- the endpoint's own `.Produces(StatusCodes.Status200OK)` carries no content-type (the real MIME
   type is decided per-document at runtime by `Results.Bytes(...)`), so NSwag has nothing to generate
   a typed stream response from. Rather than force a misleading fixed content-type onto an endpoint
   whose body genuinely varies, the download link bypasses the generated client and points a plain
   `<a href>` straight at the relative Gateway URL -- which is what a file download needs anyway (the
   browser handles it natively).
2. **Document upload failed with `UnauthorizedAccessException`** against the real running stack.
   `sitesvolume`'s `default/documents` directory is mode `700`, owned by uid 1000 -- the uid OpenEMR's
   own `apache` worker processes run as inside its container. PatientManagement's container ran as the
   Dockerfile's default `$APP_UID` (1654), which that directory's permissions give zero access to,
   regardless of group membership (no group/other bits set at all). Fixed by pinning
   `patient-management`'s container to `user: "1000:1000"` in `docker-compose.yml` -- not by loosening
   the volume's own permissions, just by making the container assume the uid that already legitimately
   owns it, the same uid OpenEMR's own writes already use.
3. **Document upload then failed a second way**, with `SELECT command denied... for column 'id' in
   table 'sequences'`. `scripts/seeds/patient-management-db.sh` granted only `UPDATE` on `sequences`
   for `UPDATE sequences SET id = LAST_INSERT_ID(id + 1)` -- its own comment claimed this was
   sufficient ("UPDATE alone covers it, since it reads the row being updated"), which is wrong about
   MariaDB's actual privilege model: a column referenced on the right-hand side of a `SET` expression
   needs `SELECT` too, even one from the row being updated. Fixed by granting `SELECT, UPDATE`.
   **This exposed a real test-coverage gap, not just a one-off bug**: `PatientDataContainerFixture`
   (the Testcontainers fixture all 57 PatientManagement integration tests run against) never creates a
   restricted least-privilege user or issues any `GRANT` at all -- every integration test connects with
   a fully-privileged user, so the real seed script's exact grant set has never been exercised by any
   automated test. This bug existed in the seed script the whole time; nothing caught it until a real
   `curl` through the real deployed stack did.

## Decision

1. **`PatientFhirView`**/`PatientSummaryResponse` gain `Pid`. **New `GET /api/v1/patients/{uuid}`**
   (`PatientEndpoints.cs`), reusing `GetPatientFhirRecordUseCase` as-is.
2. **New `Pages/Patients/Detail.razor`** (`@page "/patients/{Uuid:guid}"`), gated on sign-in the same
   way the existing pages are. Two sections: Insurance (effective-coverage table, add-policy form,
   swap form) and Advance Directives (status display/form, document table with a direct download
   link, upload form using `InputFile` read into a base64 string). Owner/authenticator id fields reuse
   `Register.razor`'s own placeholder-id pattern (ADR 0055's tracked gap, not a new decision here).
3. **`docker-compose.yml`**: `patient-management` pinned to `user: "1000:1000"`.
4. **`scripts/seeds/patient-management-db.sh`**: `sequences` grant corrected to `SELECT, UPDATE`, with
   a corrected comment explaining why `UPDATE` alone was never actually sufficient.
5. **Verified against the real running stack for every feature**, not just against Testcontainers:
   registered/reused a patient, added an insurance policy, swapped its level, set advance-directive
   status, uploaded a document, listed it, and downloaded its exact original bytes back -- all through
   the real Gateway, with the real least-privilege database user and the real shared-volume
   permissions, via `curl` (the Chrome extension was unavailable in this environment).

## Consequences

- Every PatientManagement.Api feature except patient merge (M5+-blocked) and the FHIR API (no legacy
  UI equivalent) now has a working WebApp page.
- The `sequences`-grant bug would have reached a real deployment undetected by this repository's own
  test suite. `PatientDataContainerFixture` testing against an unrestricted user, not the real
  least-privilege one `ADR 0050` actually provisions, is a real, now-named gap -- closing it (e.g.
  having the fixture create and connect as the real restricted user) is a separate, not-yet-scheduled
  follow-up, not undertaken here.
- `patient-management`'s container no longer runs as the Dockerfile's own `$APP_UID` (1654) in the
  Compose deployment -- the `user:` override in `docker-compose.yml` takes precedence. A future change
  to the shared volume's ownership (e.g. if OpenEMR's own image ever changes its worker uid) would need
  this pinned value updated to match.

## Alternatives considered

- **Give the document-download link a real OpenAPI content-type and regenerate a typed client
  method.** Rejected: the real content-type is chosen per-document at runtime; declaring one fixed
  type would misdescribe the endpoint for every document that isn't that type. A plain anchor tag is
  both simpler and more honest about what the endpoint actually returns.
- **Loosen `sitesvolume`'s permissions instead of pinning the container's uid.** Rejected: broadening
  access on a directory OpenEMR's own legacy code also reads/writes is a bigger, less-contained change
  than making one container assume an already-correct uid.
- **Fix `PatientDataContainerFixture` to test the real restricted grant set, in this same round.**
  Not undertaken: real, worth doing, but a separate scope from "extend the frontend" -- named as a
  follow-up instead of folded in here.

## Related decisions

Builds on [0055](0055-patientmanagement-api-gated-by-a-shared-secret-identity-forwarding-deferred.md)'s
placeholder-id pattern and [0060](0060-patientmanagement-is-deployed-and-reachable-through-the-gateway.md)'s
deployment. Completes PatientManagement.Api's WebApp coverage short of patient merge and the FHIR API.
