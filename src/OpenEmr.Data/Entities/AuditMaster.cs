using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class AuditMaster
{
    public long Id { get; set; }

    public long Pid { get; set; }

    /// <summary>
    /// The Id of the user who approves or denies
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 1-Pending,2-Approved,3-Denied,4-Appointment directly updated to calendar table,5-Cancelled appointment
    /// </summary>
    public sbyte ApprovalStatus { get; set; }

    public string? Comments { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime ModifiedTime { get; set; }

    public string IpAddress { get; set; } = null!;

    /// <summary>
    /// 1-new patient,2-existing patient,3-change is only in the document,4-Patient upload,5-random key,10-Appointment
    /// </summary>
    public sbyte Type { get; set; }

    public bool? IsQrdaDocument { get; set; }

    public bool? IsUnstructuredDocument { get; set; }
}
