using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ContactRelation
{
    public long Id { get; set; }

    public long ContactId { get; set; }

    public string TargetTable { get; set; } = null!;

    public long TargetId { get; set; }

    public bool? Active { get; set; }

    public string? Role { get; set; }

    public string? Relationship { get; set; }

    /// <summary>
    /// 1=highest priority
    /// </summary>
    public int? ContactPriority { get; set; }

    public bool? IsPrimaryContact { get; set; }

    public bool? IsEmergencyContact { get; set; }

    public bool? CanMakeMedicalDecisions { get; set; }

    public bool? CanReceiveMedicalInfo { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// users.id
    /// </summary>
    public long? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    /// <summary>
    /// users.id
    /// </summary>
    public long? UpdatedBy { get; set; }
}
