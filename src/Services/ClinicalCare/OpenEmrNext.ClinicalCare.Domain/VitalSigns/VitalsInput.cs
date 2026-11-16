namespace OpenEmrNext.ClinicalCare.Domain.VitalSigns;

/// <summary>
/// The raw facts a caller supplies to record vitals, before any rule has judged them. Plain data with no invariant:
/// <see cref="VitalsRecording.Create"/> is where every rule lives (DOM-2). BMI is deliberately absent: it is derived
/// (CV-5, ADR 0065 #6).
/// </summary>
public sealed record VitalsInput(
    Quantity? Weight = null,
    Quantity? Height = null,
    Quantity? Temperature = null,
    string? TemperatureMethod = null,
    int? Systolic = null,
    int? Diastolic = null,
    int? Pulse = null,
    int? Respiration = null,
    decimal? OxygenSaturation = null,
    Quantity? WaistCircumference = null,
    Quantity? HeadCircumference = null,
    string? Note = null);
