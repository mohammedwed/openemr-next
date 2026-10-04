using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Amendment
{
    /// <summary>
    /// Amendment ID
    /// </summary>
    public int AmendmentId { get; set; }

    /// <summary>
    /// Amendement request date
    /// </summary>
    public DateOnly AmendmentDate { get; set; }

    /// <summary>
    /// Amendment requested from
    /// </summary>
    public string AmendmentBy { get; set; } = null!;

    /// <summary>
    /// Amendment status accepted/rejected/null
    /// </summary>
    public string? AmendmentStatus { get; set; }

    /// <summary>
    /// Patient ID from patient_data
    /// </summary>
    public long Pid { get; set; }

    /// <summary>
    /// Amendment Details
    /// </summary>
    public string? AmendmentDesc { get; set; }

    /// <summary>
    /// references users.id for session owner
    /// </summary>
    public int CreatedBy { get; set; }

    /// <summary>
    /// references users.id for session owner
    /// </summary>
    public int? ModifiedBy { get; set; }

    /// <summary>
    /// created time
    /// </summary>
    public DateTime? CreatedTime { get; set; }

    /// <summary>
    /// modified time
    /// </summary>
    public DateTime? ModifiedTime { get; set; }
}
