using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Immunizations;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>The names of the immunization fields a correction can change, as the event, the audit row and the API report them.</summary>
public static class ImmunizationFieldNames
{
    public const string AdministeredDate = "administeredDate", Immunization = "immunization", Cvx = "cvxCode", Manufacturer = "manufacturer", Lot = "lotNumber",
        Expiration = "expirationDate", Amount = "amountAdministered", Unit = "amountUnit", Route = "route", Site = "administrationSite",
        Completion = "completionStatus", Source = "informationSource", Refusal = "refusalReason", Education = "educationDate", Vis = "visDate",
        AdministeredBy = "administeredBy", AdministeredById = "administeredById", OrderingProvider = "orderingProvider", Encounter = "encounter",
        ReasonCode = "reasonCode", ReasonDescription = "reasonDescription", Note = "note", EnteredInError = "enteredInError";
}

/// <summary>Which of an immunization's fields a correction changes: one answer, shared by the event, the audit row and the decision to write.</summary>
public static class ImmunizationChangeSet
{
    public static IReadOnlyList<string> Between(ImmunizationView current, ImmunizationFacts next)
    {
        var changed = new List<string>();
        void Check<T>(string name, T before, T after)
        {
            if (!EqualityComparer<T>.Default.Equals(before, after))
                changed.Add(name);
        }

        Check(ImmunizationFieldNames.AdministeredDate, current.AdministeredDate, next.AdministeredDate);
        Check(ImmunizationFieldNames.Immunization, current.ImmunizationId, next.ImmunizationId);
        Check(ImmunizationFieldNames.Cvx, current.CvxCode, next.CvxCode);
        Check(ImmunizationFieldNames.Manufacturer, current.Manufacturer, next.Manufacturer);
        Check(ImmunizationFieldNames.Lot, current.LotNumber, next.LotNumber);
        Check(ImmunizationFieldNames.Expiration, current.ExpirationDate, next.ExpirationDate);
        Check(ImmunizationFieldNames.Amount, current.AmountAdministered, next.AmountAdministered);
        Check(ImmunizationFieldNames.Unit, current.AmountUnit, next.AmountUnit);
        Check(ImmunizationFieldNames.Route, current.Route, next.Route);
        Check(ImmunizationFieldNames.Site, current.AdministrationSite, next.AdministrationSite);
        Check(ImmunizationFieldNames.Completion, current.CompletionStatus, next.CompletionStatus);
        Check(ImmunizationFieldNames.Source, current.InformationSource, next.InformationSource);
        Check(ImmunizationFieldNames.Refusal, current.RefusalReason, next.RefusalReason);
        Check(ImmunizationFieldNames.Education, current.EducationDate, next.EducationDate);
        Check(ImmunizationFieldNames.Vis, current.VisDate, next.VisDate);
        Check(ImmunizationFieldNames.AdministeredBy, current.AdministeredBy, next.AdministeredBy);
        Check(ImmunizationFieldNames.AdministeredById, current.AdministeredById, next.AdministeredById);
        Check(ImmunizationFieldNames.OrderingProvider, current.OrderingProvider, next.OrderingProvider);
        Check(ImmunizationFieldNames.Encounter, current.EncounterNumber, next.EncounterNumber);
        Check(ImmunizationFieldNames.ReasonCode, current.ReasonCode, next.ReasonCode);
        Check(ImmunizationFieldNames.ReasonDescription, current.ReasonDescription, next.ReasonDescription);
        Check(ImmunizationFieldNames.Note, current.Note, next.Note);
        Check(ImmunizationFieldNames.EnteredInError, current.EnteredInError, next.EnteredInError);
        return changed;
    }
}

/// <summary>Everything a caller supplies to record an immunization. <paramref name="Input"/>'s <c>Encounter</c> is ignored: <paramref name="EncounterUuid"/> is resolved here.</summary>
public sealed record RecordImmunizationCommand(Guid PatientUuid, ImmunizationInput Input, Guid? EncounterUuid, long ProviderId, RequestOrigin? Origin = null);

/// <summary>Records an immunization on a registered patient's record (M5d slice 2). Thin (ARC-4): resolve through ports, let the domain decide, commit with event and audit row. A refusal is audited as failed.</summary>
public sealed class RecordImmunizationUseCase(
    IPatientDirectory patients, IEncounterReader encounters, IImmunizationCodeLists codeLists, IImmunizationIdentityAllocator identities,
    IImmunizationRegistry registry, IAuditLog auditLog, TimeProvider clock)
{
    /// <exception cref="PatientNotFoundException">No such patient.</exception>
    /// <exception cref="EncounterNotFoundException">The linked encounter does not exist.</exception>
    /// <exception cref="ClinicalCareValidationException">A field fails validation.</exception>
    public async Task<Immunization> ExecuteAsync(RecordImmunizationCommand command, CancellationToken ct)
    {
        try
        {
            return await RecordAsync(command, ct);
        }
        catch (Exception ex) when (ex is PatientNotFoundException or EncounterNotFoundException or ClinicalCareValidationException)
        {
            await auditLog.AppendAsync(
                new AuditEntry(
                    clock.GetUtcNow().UtcDateTime, new AuditActor($"user:{command.ProviderId}", command.ProviderId, Verified: false),
                    AuditActions.ImmunizationRecord, command.PatientUuid, AuditOutcome.Failed, command.Origin?.CorrelationId, command.Origin?.SourceAddress),
                ct);
            throw;
        }
    }

    private async Task<Immunization> RecordAsync(RecordImmunizationCommand command, CancellationToken ct)
    {
        var patient = await patients.FindAsync(command.PatientUuid, ct) ?? throw new PatientNotFoundException(command.PatientUuid);
        var linked = await ResolveEncounterAsync(encounters, command.EncounterUuid, ct);
        var codes = await codeLists.GetCodesAsync(ct);

        var nowLocal = clock.GetLocalNow().DateTime;   // legacy datetime columns hold server-local time (LEG-11)
        var nowUtc = clock.GetUtcNow().UtcDateTime;

        var facts = ImmunizationFacts.Create(command.Input with { Encounter = linked }, patient.Pid, nowLocal, codes);
        var immunization = Immunization.Record(patient, facts, command.ProviderId, identities.Allocate(), nowLocal);

        await registry.RegisterAsync(
            immunization, new ImmunizationRecordedEvent(immunization.Uuid, patient.Uuid, patient.Pid, nowUtc),
            new AuditEntry(
                nowUtc, new AuditActor($"user:{command.ProviderId}", command.ProviderId, Verified: false), AuditActions.ImmunizationRecord, immunization.Uuid,
                AuditOutcome.Success, command.Origin?.CorrelationId, command.Origin?.SourceAddress),
            ct);

        return immunization;
    }

    internal static async Task<ImmunizationEncounter?> ResolveEncounterAsync(IEncounterReader encounters, Guid? encounterUuid, CancellationToken ct)
    {
        if (encounterUuid is not { } uuid)
            return null;

        var encounter = await encounters.FindAsync(uuid, ct) ?? throw new EncounterNotFoundException(uuid);
        // An encounter that cannot name its patient or number is reported by the rules as a number or patient mismatch, never stored.
        return new ImmunizationEncounter(encounter.Number ?? 0, encounter.PatientPid ?? 0);
    }
}

/// <summary>Everything a caller supplies to correct an immunization: which, the version read (<c>ETag</c>), and the whole editable set.</summary>
public sealed record CorrectImmunizationCommand(
    Guid ImmunizationUuid, IReadOnlyList<ContentVersion> Offered, ImmunizationInput Input, Guid? EncounterUuid, RequestOrigin? Origin = null);

public sealed record ImmunizationCorrectionResult(ImmunizationView Immunization, IReadOnlyList<string> ChangedFields);

/// <summary>
/// Corrects an immunization's editable set (ADR 0072). Read, check the caller's version, validate, diff, then hand to <see cref="IImmunizationUpdater"/>, which verifies the version again under a row lock. A refusal is audited as failed.
/// </summary>
public sealed class CorrectImmunizationUseCase(
    IImmunizationReader immunizations, IEncounterReader encounters, IImmunizationCodeLists codeLists, IImmunizationUpdater updater, IAuditLog auditLog, TimeProvider clock)
{
    public async Task<ImmunizationCorrectionResult> ExecuteAsync(CorrectImmunizationCommand command, CancellationToken ct)
    {
        try
        {
            return await CorrectAsync(command, ct);
        }
        catch (Exception ex) when (ex is ImmunizationNotFoundException or EncounterNotFoundException or ImmunizationConflictException or ClinicalCareValidationException)
        {
            await auditLog.AppendAsync(
                new AuditEntry(
                    clock.GetUtcNow().UtcDateTime, AuditActor.Unknown, AuditActions.ImmunizationCorrect, command.ImmunizationUuid, AuditOutcome.Failed,
                    command.Origin?.CorrelationId, command.Origin?.SourceAddress),
                ct);
            throw;
        }
    }

    private async Task<ImmunizationCorrectionResult> CorrectAsync(CorrectImmunizationCommand command, CancellationToken ct)
    {
        var current = await immunizations.FindAsync(command.ImmunizationUuid, ct) ?? throw new ImmunizationNotFoundException(command.ImmunizationUuid);

        // The precondition comes before the request is judged (as for every update, ADR 0068).
        var currentVersion = current.Version();
        if (!command.Offered.Contains(currentVersion))
            throw new ImmunizationConflictException(command.ImmunizationUuid, currentVersion);

        // A row whose patient is unknown cannot be checked against an encounter or corrected safely.
        var pid = current.PatientPid ?? throw new ImmunizationNotFoundException(command.ImmunizationUuid);

        var linked = await RecordImmunizationUseCase.ResolveEncounterAsync(encounters, command.EncounterUuid, ct);
        var codes = await codeLists.GetCodesAsync(ct);
        var facts = ImmunizationFacts.Create(command.Input with { Encounter = linked }, pid, clock.GetLocalNow().DateTime, codes);

        var changed = ImmunizationChangeSet.Between(current, facts);
        if (changed.Count == 0)
            return new ImmunizationCorrectionResult(current, changed);

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var outcome = await updater.UpdateAsync(
            new ImmunizationUpdateRequest(
                command.ImmunizationUuid, pid, currentVersion, facts,
                new ImmunizationCorrectedEvent(current.Uuid, current.PatientUuid, current.PatientPid, changed, nowUtc),
                new AuditEntry(
                    nowUtc, AuditActor.Unknown, AuditActions.ImmunizationCorrect, command.ImmunizationUuid, AuditOutcome.Success,
                    command.Origin?.CorrelationId, command.Origin?.SourceAddress, changed)),
            ct);

        return outcome.Status switch
        {
            UpdateStatus.Updated => new ImmunizationCorrectionResult(
                await immunizations.FindAsync(command.ImmunizationUuid, ct) ?? throw new ImmunizationNotFoundException(command.ImmunizationUuid), changed),
            UpdateStatus.Conflict => throw new ImmunizationConflictException(command.ImmunizationUuid, outcome.Version),
            _ => throw new ImmunizationNotFoundException(command.ImmunizationUuid)
        };
    }
}

/// <summary>Reads one immunization.</summary>
public sealed class GetImmunizationUseCase(IImmunizationReader immunizations)
{
    public Task<ImmunizationView?> ExecuteAsync(Guid immunizationUuid, CancellationToken ct) => immunizations.FindAsync(immunizationUuid, ct);
}

/// <summary>Lists a registered patient's immunizations, most recently administered first.</summary>
public sealed class ListPatientImmunizationsUseCase(IPatientDirectory patients, IImmunizationReader immunizations)
{
    /// <exception cref="PatientNotFoundException">No such patient.</exception>
    /// <exception cref="ClinicalCareValidationException">The page bounds are outside what is allowed.</exception>
    public async Task<ImmunizationPage> ExecuteAsync(Guid patientUuid, int? limit, int? offset, CancellationToken ct)
    {
        var page = PageRequest.Create(limit, offset);   // bounds first: no round trip for an unbounded request
        var patient = await patients.FindAsync(patientUuid, ct) ?? throw new PatientNotFoundException(patientUuid);
        return await immunizations.ListForPatientAsync(patient, page, ct);
    }
}
