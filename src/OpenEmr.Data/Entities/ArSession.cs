using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ArSession
{
    public uint SessionId { get; set; }

    /// <summary>
    /// 0=pt else references insurance_companies.id
    /// </summary>
    public int PayerId { get; set; }

    /// <summary>
    /// references users.id for session owner
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 0=no, 1=yes
    /// </summary>
    public bool Closed { get; set; }

    /// <summary>
    /// check or EOB number
    /// </summary>
    public string Reference { get; set; } = null!;

    public DateOnly? CheckDate { get; set; }

    public DateOnly? DepositDate { get; set; }

    public decimal PayTotal { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime ModifiedTime { get; set; }

    public decimal GlobalAmount { get; set; }

    public string PaymentType { get; set; } = null!;

    public string? Description { get; set; }

    public string AdjustmentCode { get; set; } = null!;

    public DateOnly PostToDate { get; set; }

    public long PatientId { get; set; }

    public string PaymentMethod { get; set; } = null!;
}
