using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormGroupsEncounter
{
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    public string? Reason { get; set; }

    public string? Facility { get; set; }

    public int FacilityId { get; set; }

    public long? GroupId { get; set; }

    public long? Encounter { get; set; }

    public DateTime? OnsetDate { get; set; }

    public string? Sensitivity { get; set; }

    public string? BillingNote { get; set; }

    /// <summary>
    /// event category from openemr_postcalendar_categories
    /// </summary>
    public int PcCatid { get; set; }

    /// <summary>
    /// 0=none, 1=ins1, 2=ins2, etc
    /// </summary>
    public int LastLevelBilled { get; set; }

    /// <summary>
    /// 0=none, 1=ins1, 2=ins2, etc
    /// </summary>
    public int LastLevelClosed { get; set; }

    public DateOnly? LastStmtDate { get; set; }

    public int StmtCount { get; set; }

    /// <summary>
    /// default and main provider for this visit
    /// </summary>
    public int? ProviderId { get; set; }

    /// <summary>
    /// supervising provider, if any, for this visit
    /// </summary>
    public int? SupervisorId { get; set; }

    public string InvoiceRefno { get; set; } = null!;

    public string ReferralSource { get; set; } = null!;

    public int BillingFacility { get; set; }

    public string? ExternalId { get; set; }

    public sbyte? PosCode { get; set; }

    public string? Counselors { get; set; }

    public int? ApptId { get; set; }
}
