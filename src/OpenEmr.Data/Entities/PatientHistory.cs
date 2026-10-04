using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class PatientHistory
{
    public long Id { get; set; }

    public byte[]? Uuid { get; set; }

    public DateTime Date { get; set; }

    public string? CareTeamProvider { get; set; }

    public string? CareTeamFacility { get; set; }

    public long Pid { get; set; }

    public string? HistoryTypeKey { get; set; }

    public string? PreviousNamePrefix { get; set; }

    public string? PreviousNameFirst { get; set; }

    public string? PreviousNameMiddle { get; set; }

    public string? PreviousNameLast { get; set; }

    public string? PreviousNameSuffix { get; set; }

    public DateOnly? PreviousNameEnddate { get; set; }

    /// <summary>
    /// users.id the user that first created this record
    /// </summary>
    public long? CreatedBy { get; set; }
}
