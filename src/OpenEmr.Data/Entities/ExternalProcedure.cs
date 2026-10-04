using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ExternalProcedure
{
    public int EpId { get; set; }

    public DateOnly? EpDate { get; set; }

    public string? EpCodeType { get; set; }

    public string? EpCode { get; set; }

    public int? EpPid { get; set; }

    public int? EpEncounter { get; set; }

    public string? EpCodeText { get; set; }

    public string? EpFacilityId { get; set; }

    public string? EpExternalId { get; set; }
}
