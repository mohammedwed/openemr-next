using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class AmendmentsHistory
{
    /// <summary>
    /// Amendment ID
    /// </summary>
    public int AmendmentId { get; set; }

    /// <summary>
    /// Amendment requested from
    /// </summary>
    public string? AmendmentNote { get; set; }

    /// <summary>
    /// Amendment Request Status
    /// </summary>
    public string? AmendmentStatus { get; set; }

    /// <summary>
    /// references users.id for session owner
    /// </summary>
    public int CreatedBy { get; set; }

    /// <summary>
    /// created time
    /// </summary>
    public DateTime? CreatedTime { get; set; }
}
