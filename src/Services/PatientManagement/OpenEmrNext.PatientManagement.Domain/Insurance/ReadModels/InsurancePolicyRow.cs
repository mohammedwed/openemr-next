namespace OpenEmrNext.PatientManagement.Domain.Insurance.ReadModels;

/// <summary>
/// One raw <c>insurance_data</c> row, exactly as stored -- dates as the literal strings a DATE
/// column holds (zeros and partial dates included, e.g. <c>"2026-05-00"</c>), not parsed. The
/// ranking rule in <see cref="EffectiveInsuranceRule"/> compares these as stored, the same way SQL
/// compares a DATE column; parsing only happens afterward, for the winning row's own output.
/// </summary>
/// <param name="Id">insurance_data.id</param>
/// <param name="Uuid">insurance_data.uuid, or null when the row has none</param>
/// <param name="Type">insurance_data.type -- expected to be "primary"/"secondary"/"tertiary"</param>
/// <param name="Provider">insurance_data.provider, the insurance company's id, as stored (may be blank, "0", or unparseable)</param>
/// <param name="Date">insurance_data.date (the policy's start), as stored -- "" for blank</param>
/// <param name="DateEnd">insurance_data.date_end, as stored -- "" for blank</param>
public sealed record InsurancePolicyRow(
    long Id,
    Guid? Uuid,
    string? Type,
    string? Provider,
    string Date,
    string DateEnd);
