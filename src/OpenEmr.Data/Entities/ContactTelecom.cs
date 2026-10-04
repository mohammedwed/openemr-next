using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ContactTelecom
{
    public long Id { get; set; }

    public long ContactId { get; set; }

    /// <summary>
    /// Specify preferred order of use (1 = highest)
    /// </summary>
    public int? Rank { get; set; }

    /// <summary>
    /// FK to list_options.option_id for list_id telecom_systems [phone, fax, email, pager, url, sms, other]
    /// </summary>
    public string? System { get; set; }

    /// <summary>
    /// FK to list_options.option_id for list_id telecom_uses [home, work, temp, old, mobile]
    /// </summary>
    public string? Use { get; set; }

    public string? Value { get; set; }

    /// <summary>
    /// A=active,I=inactive
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Y=yes,N=no
    /// </summary>
    public string? IsPrimary { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Date the telecom became active
    /// </summary>
    public DateTime? PeriodStart { get; set; }

    /// <summary>
    /// Date the telecom became deactivated
    /// </summary>
    public DateTime? PeriodEnd { get; set; }

    /// <summary>
    /// [Values: ???, etc]
    /// </summary>
    public string? InactivatedReason { get; set; }

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
