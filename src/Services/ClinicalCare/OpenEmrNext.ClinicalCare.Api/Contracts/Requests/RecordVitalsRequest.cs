using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Requests;

/// <summary>
/// A measurement that carries its unit (ADR 0065): <c>{ "value": 70, "unit": "kg" }</c>. The unit is mandatory, so the
/// service never has to guess whether <c>70</c> means pounds or kilograms.
/// </summary>
/// <param name="Value">The number, in <paramref name="Unit"/>.</param>
/// <param name="Unit">
/// Weight: <c>lb</c> or <c>kg</c>. Height, waist and head circumference: <c>in</c> or <c>cm</c>. Temperature:
/// <c>degF</c> or <c>degC</c>.
/// </param>
public sealed record QuantityRequest(
    decimal Value,
    [Required(ErrorMessage = "A unit is required for every measurement.")] string Unit);

/// <summary>
/// The body of <c>POST /api/v1/encounters/{encounterUuid}/vitals</c>. At least one measurement is required (CV-2).
/// BMI is not accepted: it is derived from weight and height (CV-5). Bounds are enforced by the domain (CV-3); the
/// attributes here only fail the structural checks earlier and feed the OpenAPI document (ARC-11).
/// </summary>
/// <param name="Weight">Body weight, in <c>lb</c> or <c>kg</c>.</param>
/// <param name="Height">Body height, in <c>in</c> or <c>cm</c>.</param>
/// <param name="Temperature">Body temperature, in <c>degF</c> or <c>degC</c>.</param>
/// <param name="TemperatureMethod">How the temperature was taken. Kept only if a temperature is given (CV-7).</param>
/// <param name="BloodPressureSystolic">Systolic blood pressure, whole mm[Hg].</param>
/// <param name="BloodPressureDiastolic">Diastolic blood pressure, whole mm[Hg].</param>
/// <param name="Pulse">Pulse, whole beats per minute.</param>
/// <param name="Respiration">Respiration, whole breaths per minute.</param>
/// <param name="OxygenSaturation">Oxygen saturation, percent.</param>
/// <param name="WaistCircumference">Waist circumference, in <c>in</c> or <c>cm</c>.</param>
/// <param name="HeadCircumference">Head circumference, in <c>in</c> or <c>cm</c>.</param>
/// <param name="Note">A free-text note, up to 255 characters.</param>
/// <param name="ProviderId">
/// Ignored: the acting provider is the signed-in user the gateway verified (ADR 0075), so a caller can no longer name who is acting. Kept optional so existing callers do not break; it will be removed in a later contract version.
/// </param>
public sealed record RecordVitalsRequest(
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
    [StringLength(255, ErrorMessage = "Note must be at most 255 characters.")] string? Note,
    [Range(1, int.MaxValue, ErrorMessage = "ProviderId must identify the acting provider.")] long? ProviderId);
