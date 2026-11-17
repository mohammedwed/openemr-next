using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Requests;

/// <summary>
/// The body of <c>POST /api/v1/immunizations</c> (M5d slice 2, ADR 0072). The length bounds restate <c>ImmunizationFacts.Create</c>'s rules with the <b>same messages</b> (ARC-11); the domain factory remains the one
/// place that enforces them, including the code lists, the dates and the two <c>text</c> fields, which are limited in bytes, not characters.
/// </summary>
/// <param name="PatientUuid">The patient's uuid. Unknown patients get 404.</param>
/// <param name="AdministeredDate">When it was given (server-local time, as legacy; a zoned date is converted). Required, between 1900-01-01 and tomorrow.</param>
/// <param name="ImmunizationId">A code of the configured <c>immunizations</c> list. At least one of this and <paramref name="CvxCode"/> is required.</param>
/// <param name="CvxCode">The CVX vaccine code, up to 64 characters.</param>
/// <param name="AmountAdministered">Greater than 0, at most 100000, stored to four places. Needs <paramref name="AmountUnit"/>.</param>
/// <param name="AmountUnit">A configured <c>drug_units</c> code.</param>
/// <param name="Route">A configured <c>drug_route</c> code.</param>
/// <param name="AdministrationSite">A configured <c>immunization_administered_site</c> code.</param>
/// <param name="CompletionStatus">A configured <c>Immunization_Completion_Status</c> code (for example <c>Completed</c> or <c>Refused</c>).</param>
/// <param name="InformationSource">A configured <c>immunization_informationsource</c> code.</param>
/// <param name="RefusalReason">A configured <c>immunization_refusal_reason</c> code.</param>
/// <param name="ExpirationDate">The vaccine's expiration date. An expired vaccine is accepted: giving one is a reportable event, not an invalid record.</param>
/// <param name="EducationDate">When the patient was educated. <paramref name="VisDate"/>: the Vaccine Information Statement's date.</param>
/// <param name="AdministeredBy">Free-text name of who gave it (up to 255 characters); <paramref name="AdministeredById"/> their user id, if a user.</param>
/// <param name="OrderingProvider">The ordering provider's user id.</param>
/// <param name="EncounterUuid">An encounter of the same patient to link it to, or <c>null</c>. Unknown encounters get 404.</param>
/// <param name="EnteredInError">Marks the record as entered in error: legacy's non-destructive way to void a mistaken entry.</param>
/// <param name="ProviderId">Ignored: the acting provider is the signed-in user the gateway verified (ADR 0075), so a caller can no longer name who is acting. Kept optional so existing callers do not break; it will be removed in a later contract version..</param>
public sealed record RecordImmunizationRequest(
    [Required] Guid PatientUuid,
    [Required(ErrorMessage = "An administered date is required.")] DateTime? AdministeredDate,
    [Range(1, int.MaxValue, ErrorMessage = "ImmunizationId must be a positive code.")] int? ImmunizationId,
    [StringLength(64, ErrorMessage = "CVX code must be at most 64 characters.")] string? CvxCode,
    [StringLength(100, ErrorMessage = "Manufacturer must be at most 100 characters.")] string? Manufacturer,
    [StringLength(50, ErrorMessage = "Lot number must be at most 50 characters.")] string? LotNumber,
    DateTime? ExpirationDate,
    decimal? AmountAdministered,
    [StringLength(50, ErrorMessage = "Amount unit must be at most 50 characters.")] string? AmountUnit,
    [StringLength(100, ErrorMessage = "Route must be at most 100 characters.")] string? Route,
    [StringLength(100, ErrorMessage = "Administration site must be at most 100 characters.")] string? AdministrationSite,
    [StringLength(50, ErrorMessage = "Completion status must be at most 50 characters.")] string? CompletionStatus,
    [StringLength(31, ErrorMessage = "Information source must be at most 31 characters.")] string? InformationSource,
    [StringLength(31, ErrorMessage = "Refusal reason must be at most 31 characters.")] string? RefusalReason,
    DateTime? EducationDate,
    DateTime? VisDate,
    [StringLength(255, ErrorMessage = "Administered by must be at most 255 characters.")] string? AdministeredBy,
    long? AdministeredById,
    long? OrderingProvider,
    Guid? EncounterUuid,
    [StringLength(31, ErrorMessage = "Reason code must be at most 31 characters.")] string? ReasonCode,
    string? ReasonDescription,
    string? Note,
    bool EnteredInError,
    [Range(1, int.MaxValue, ErrorMessage = "ProviderId must identify the acting provider.")] long? ProviderId);

/// <summary>
/// The body of <c>PUT /api/v1/immunizations/{uuid}</c>: the <b>whole</b> editable set, replacing what is stored (a field left out is cleared), conditional on the <c>ETag</c> in <c>If-Match</c>. Same fields and rules as
/// <see cref="RecordImmunizationRequest"/> minus the patient (which cannot change) and the provider.
/// </summary>
public sealed record CorrectImmunizationRequest(
    [Required(ErrorMessage = "An administered date is required.")] DateTime? AdministeredDate,
    [Range(1, int.MaxValue, ErrorMessage = "ImmunizationId must be a positive code.")] int? ImmunizationId,
    [StringLength(64, ErrorMessage = "CVX code must be at most 64 characters.")] string? CvxCode,
    [StringLength(100, ErrorMessage = "Manufacturer must be at most 100 characters.")] string? Manufacturer,
    [StringLength(50, ErrorMessage = "Lot number must be at most 50 characters.")] string? LotNumber,
    DateTime? ExpirationDate,
    decimal? AmountAdministered,
    [StringLength(50, ErrorMessage = "Amount unit must be at most 50 characters.")] string? AmountUnit,
    [StringLength(100, ErrorMessage = "Route must be at most 100 characters.")] string? Route,
    [StringLength(100, ErrorMessage = "Administration site must be at most 100 characters.")] string? AdministrationSite,
    [StringLength(50, ErrorMessage = "Completion status must be at most 50 characters.")] string? CompletionStatus,
    [StringLength(31, ErrorMessage = "Information source must be at most 31 characters.")] string? InformationSource,
    [StringLength(31, ErrorMessage = "Refusal reason must be at most 31 characters.")] string? RefusalReason,
    DateTime? EducationDate,
    DateTime? VisDate,
    [StringLength(255, ErrorMessage = "Administered by must be at most 255 characters.")] string? AdministeredBy,
    long? AdministeredById,
    long? OrderingProvider,
    Guid? EncounterUuid,
    [StringLength(31, ErrorMessage = "Reason code must be at most 31 characters.")] string? ReasonCode,
    string? ReasonDescription,
    string? Note,
    bool EnteredInError);
