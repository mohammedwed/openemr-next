using OpenEmrNext.ClinicalCare.Api.Endpoints;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

/// <summary>A measurement with its unit, in the unit system the read asked for (ADR 0065).</summary>
/// <param name="Value">The number.</param>
/// <param name="Unit"><c>lb</c>, <c>kg</c>, <c>in</c>, <c>cm</c>, <c>degF</c> or <c>degC</c>.</param>
public sealed record QuantityResponse(decimal Value, string Unit);

/// <summary>The body of a successful <c>POST /api/v1/encounters/{encounterUuid}/vitals</c>.</summary>
/// <param name="Uuid">The vitals' public identity.</param>
/// <param name="EncounterUuid">The encounter they belong to.</param>
/// <param name="Bmi">The derived BMI in kg/m², when weight and height were both given.</param>
/// <param name="BmiStatus">The BMI category label, when there is one.</param>
public sealed record VitalsRecordedResponse(Guid Uuid, Guid EncounterUuid, decimal? Bmi, string? BmiStatus);

/// <summary>
/// One set of vitals. Unit-bearing values are <see cref="QuantityResponse"/>s in the requested unit system; the others
/// are plain numbers in their one unit: blood pressure mm[Hg], pulse and respiration per minute, oxygen saturation
/// percent, BMI kg/m². A value that was not recorded is <c>null</c>.
/// </summary>
public sealed record VitalsResponse(
    Guid Uuid,
    DateTime? RecordedAt,
    string? RecordedBy,
    QuantityResponse? Weight,
    QuantityResponse? Height,
    QuantityResponse? Temperature,
    string? TemperatureMethod,
    int? BloodPressureSystolic,
    int? BloodPressureDiastolic,
    int? Pulse,
    int? Respiration,
    decimal? OxygenSaturation,
    QuantityResponse? WaistCircumference,
    QuantityResponse? HeadCircumference,
    decimal? Bmi,
    string? BmiStatus,
    string? Note,
    string? ETag = null)
{
    public static VitalsResponse From(VitalsView view, UnitSystem system) => new(
        view.Uuid, view.RecordedAt, view.RecordedBy,
        Quantity(MeasurementKind.Weight, view.WeightPounds, system),
        Quantity(MeasurementKind.Height, view.HeightInches, system),
        Quantity(MeasurementKind.Temperature, view.TemperatureFahrenheit, system),
        view.TemperatureMethod, view.Systolic, view.Diastolic, view.Pulse, view.Respiration, view.OxygenSaturation,
        Quantity(MeasurementKind.WaistCircumference, view.WaistInches, system),
        Quantity(MeasurementKind.HeadCircumference, view.HeadInches, system),
        view.Bmi, view.BmiStatus, view.Note, Preconditions.ETagOf(view.LastUpdated));

    private static QuantityResponse? Quantity(MeasurementKind kind, decimal? stored, UnitSystem system) =>
        stored is null
            ? null
            : new QuantityResponse(UnitConversion.FromStored(kind, stored.Value, system),
                UnitWire.Name(UnitConversion.DisplayUnit(kind, system)));
}

/// <summary>One page of an encounter's vitals, newest first (API-6).</summary>
public sealed record VitalsListResponse(IReadOnlyList<VitalsResponse> Items, int Total, int Limit, int Offset);
