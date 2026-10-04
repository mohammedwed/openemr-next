using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class OpenemrPostcalendarEvent
{
    public uint PcEid { get; set; }

    public int PcCatid { get; set; }

    public uint PcMultiple { get; set; }

    public string? PcAid { get; set; }

    public string? PcPid { get; set; }

    public int? PcGid { get; set; }

    public string? PcTitle { get; set; }

    public DateTime? PcTime { get; set; }

    public string? PcHometext { get; set; }

    public int? PcComments { get; set; }

    public uint? PcCounter { get; set; }

    public int PcTopic { get; set; }

    public string? PcInformant { get; set; }

    public DateOnly PcEventDate { get; set; }

    public DateOnly? PcEndDate { get; set; }

    public long PcDuration { get; set; }

    public int PcRecurrtype { get; set; }

    public string? PcRecurrspec { get; set; }

    public int PcRecurrfreq { get; set; }

    public TimeOnly? PcStartTime { get; set; }

    public TimeOnly? PcEndTime { get; set; }

    public int PcAlldayevent { get; set; }

    public string? PcLocation { get; set; }

    public string? PcConttel { get; set; }

    public string? PcContname { get; set; }

    public string? PcContemail { get; set; }

    public string? PcWebsite { get; set; }

    public string? PcFee { get; set; }

    public int PcEventstatus { get; set; }

    public int PcSharing { get; set; }

    public string? PcLanguage { get; set; }

    public string PcApptstatus { get; set; } = null!;

    public int PcPrefcatid { get; set; }

    /// <summary>
    /// facility id for this event
    /// </summary>
    public int PcFacility { get; set; }

    public string PcSendalertsms { get; set; } = null!;

    public string PcSendalertemail { get; set; } = null!;

    public short PcBillingLocation { get; set; }

    public string PcRoom { get; set; } = null!;

    public byte[]? Uuid { get; set; }
}
