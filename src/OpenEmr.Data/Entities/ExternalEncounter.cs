using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ExternalEncounter
{
    public int EeId { get; set; }

    public DateOnly? EeDate { get; set; }

    public int? EePid { get; set; }

    public string? EeProviderId { get; set; }

    public string? EeFacilityId { get; set; }

    public string? EeEncounterDiagnosis { get; set; }

    public string? EeExternalId { get; set; }
}
