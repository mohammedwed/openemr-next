using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.Rules;

/// <summary>
/// The field rules an encounter's editable facts obey, written once and used by both <see cref="EncounterCreation"/> and
/// <see cref="EncounterChange"/> so a create and an update can never disagree about what is valid (one owner per rule, DOM-2).
/// Rule ids as in docs/clinical-care/01-create-encounter-slice.md section 3 (CC-2, CC-3, CC-8, CC-10 to CC-14, CC-17).
/// </summary>
internal static class EncounterFieldRules
{
    /// <summary>What the rules resolved from the reference data: the class to store, the facility's name, the place of service.</summary>
    internal readonly record struct Resolved(string? ClassCode, string? FacilityName, int? PosCode);

    /// <summary>
    /// Checks every field and collects every violation into <paramref name="errors"/> (DOM-3); never throws. The order of the
    /// checks is the order of the messages, which callers and tests rely on.
    /// </summary>
    /// <param name="sensitivity">Checked when not <c>null</c>; an update does not touch it (F-CC-2) and passes <c>null</c>.</param>
    /// <param name="providerMessage">The message for a provider id outside the column's range: the same rule reads differently for who acted and whose encounter it is.</param>
    public static Resolved Validate(
        string? classCode, int calendarCategoryId, string? reason, string? sensitivity, long providerId, string providerMessage,
        DateTime? date, DateTime? onsetDate, int facilityId, int? posCode, DateTime now,
        EncounterReferenceData reference, List<string> errors)
    {
        // CC-3
        var resolvedClass = EncounterClassRule.Resolve(classCode, reference.Classes, errors);

        // CC-2: legacy requires pc_catid (EncounterValidator); the column's own default is 5 but the validator never lets a blank through.
        if (calendarCategoryId <= 0)
            errors.Add("A calendar category is required.");

        // CC-12 (new)
        if (reason is { Length: > EncounterCreation.MaxReasonLength })
            errors.Add($"Reason must be at most {EncounterCreation.MaxReasonLength} characters.");

        // CC-14: form_encounter.sensitivity is varchar(30).
        if (sensitivity is { Length: > EncounterCreation.MaxSensitivityLength })
            errors.Add($"Sensitivity must be at most {EncounterCreation.MaxSensitivityLength} characters.");

        // CC-10 (new): legacy stores 0 when nobody is signed in; this service needs an identifiable provider. The upper bound is the
        // column's own (review finding R-1).
        if (providerId is <= 0 or > EncounterCreation.MaxProviderId)
            errors.Add(providerMessage);

        // CC-17 (new)
        if (date is { } given && (given < EncounterCreation.MinDate || given > now + EncounterCreation.MaxFutureOffset))
            errors.Add($"Date must be between {EncounterCreation.MinDate:yyyy-MM-dd} and one year from now.");

        if (onsetDate is { } onset && (onset < EncounterCreation.MinDate || onset > now + EncounterCreation.MaxFutureOffset))
            errors.Add($"Onset date must be between {EncounterCreation.MinDate:yyyy-MM-dd} and one year from now.");

        // CC-8 and CC-13
        string? facilityName = null;
        var resolvedPos = posCode;
        if (facilityId < 0)
        {
            errors.Add("FacilityId cannot be negative.");
        }
        else if (facilityId > 0)
        {
            if (reference.Facility is { } facility && facility.Id == facilityId)
            {
                // Legacy copies the facility's name into form_encounter.facility (save.php:87-88) and defaults pos_code from
                // facility.pos_code (EncounterService::getPosCode).
                facilityName = facility.Name;
                resolvedPos ??= facility.PosCode;
            }
            else
            {
                // (new) decision D-CC-3: legacy would store a null name for an unknown facility.
                errors.Add($"Facility {facilityId} does not exist.");
            }
        }

        if (resolvedPos is { } pos && (pos < 1 || pos > EncounterCreation.MaxPosCode))
            errors.Add($"PosCode must be between 1 and {EncounterCreation.MaxPosCode}.");

        return new Resolved(resolvedClass, facilityName, resolvedPos);
    }
}
