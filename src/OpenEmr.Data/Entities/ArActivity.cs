using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ArActivity
{
    public int Pid { get; set; }

    public int Encounter { get; set; }

    /// <summary>
    /// Ar_activity sequence_no, incremented in code
    /// </summary>
    public uint SequenceNo { get; set; }

    public string CodeType { get; set; } = null!;

    /// <summary>
    /// empty means claim level
    /// </summary>
    public string Code { get; set; } = null!;

    public string Modifier { get; set; } = null!;

    /// <summary>
    /// 0=pt, 1=ins1, 2=ins2, etc
    /// </summary>
    public int PayerType { get; set; }

    public DateTime PostTime { get; set; }

    /// <summary>
    /// references users.id
    /// </summary>
    public int PostUser { get; set; }

    /// <summary>
    /// references ar_session.session_id
    /// </summary>
    public uint SessionId { get; set; }

    /// <summary>
    /// adjustment reasons go here
    /// </summary>
    public string Memo { get; set; } = null!;

    /// <summary>
    /// either pay or adj will always be 0
    /// </summary>
    public decimal PayAmount { get; set; }

    public decimal AdjAmount { get; set; }

    public DateTime ModifiedTime { get; set; }

    public string FollowUp { get; set; } = null!;

    public string? FollowUpNote { get; set; }

    public string AccountCode { get; set; } = null!;

    /// <summary>
    /// Use as needed to show the primary payer adjustment reason code
    /// </summary>
    public string? ReasonCode { get; set; }

    /// <summary>
    /// NULL if active, otherwise when voided
    /// </summary>
    public DateTime? Deleted { get; set; }

    /// <summary>
    /// Posting date if specified at payment time
    /// </summary>
    public DateOnly? PostDate { get; set; }

    /// <summary>
    /// CLP07 from the payer 835
    /// </summary>
    public string? PayerClaimNumber { get; set; }
}
