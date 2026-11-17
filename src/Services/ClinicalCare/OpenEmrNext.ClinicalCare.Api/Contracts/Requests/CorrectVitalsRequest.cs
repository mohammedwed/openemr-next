using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Requests;

/// <summary>
/// The body of <c>PUT /api/v1/encounters/{encounterUuid}/vitals/{vitalsUuid}</c>: the vitals' <b>whole measurement set</b>, which replaces what is stored. The measurements and their bounds are
/// exactly <see cref="RecordVitalsRequest"/>'s, with the same messages (ARC-11); the domain stays the one place that enforces them. A measurement left out is a measurement <b>not recorded</b>,
/// so a correction can remove a reading, and must still leave at least one.
/// <para>
/// There is no <c>providerId</c>: a correction does not say who is making it (the editor is not identified until identity forwarding exists, ADR 0069 #5, and the audit row says so), and who recorded
/// the vitals and when cannot be changed. The BMI is recomputed from the corrected weight and height, never sent. The request must carry the <c>ETag</c> the caller read in an <c>If-Match</c> header:
/// 428 without it, 412 if the vitals have since changed.
/// </para>
/// </summary>
public sealed record CorrectVitalsRequest(
    QuantityRequest? Weight,
    QuantityRequest? Height,
    QuantityRequest? Temperature,
    [StringLength(255, ErrorMessage = "Temperature method must be at most 255 characters.")] string? TemperatureMethod,
    [Range(1, 400, ErrorMessage = "Systolic blood pressure must be between 1 and 400.")] int? BloodPressureSystolic,
    [Range(1, 400, ErrorMessage = "Diastolic blood pressure must be between 1 and 400.")] int? BloodPressureDiastolic,
    [Range(1, 400, ErrorMessage = "Pulse must be between 1 and 400.")] int? Pulse,
    [Range(1, 150, ErrorMessage = "Respiration must be between 1 and 150.")] int? Respiration,
    decimal? OxygenSaturation,
    QuantityRequest? WaistCircumference,
    QuantityRequest? HeadCircumference,
    [StringLength(255, ErrorMessage = "Note must be at most 255 characters.")] string? Note);
