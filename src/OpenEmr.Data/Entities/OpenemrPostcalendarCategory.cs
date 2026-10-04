using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class OpenemrPostcalendarCategory
{
    public uint PcCatid { get; set; }

    public string? PcConstantId { get; set; }

    public string? PcCatname { get; set; }

    public string? PcCatcolor { get; set; }

    public string? PcCatdesc { get; set; }

    public int PcRecurrtype { get; set; }

    public DateOnly? PcEnddate { get; set; }

    public string? PcRecurrspec { get; set; }

    public int PcRecurrfreq { get; set; }

    public long PcDuration { get; set; }

    public bool PcEndDateFlag { get; set; }

    public int? PcEndDateType { get; set; }

    public int PcEndDateFreq { get; set; }

    public bool PcEndAllDay { get; set; }

    public int PcDailylimit { get; set; }

    /// <summary>
    /// Used in grouping categories
    /// </summary>
    public int PcCattype { get; set; }

    public bool? PcActive { get; set; }

    public int PcSeq { get; set; }

    public string AcoSpec { get; set; } = null!;

    public DateTime PcLastUpdated { get; set; }
}
