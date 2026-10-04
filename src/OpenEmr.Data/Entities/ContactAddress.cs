using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ContactAddress
{
    public long Id { get; set; }

    public long ContactId { get; set; }

    public long AddressId { get; set; }

    public int? Priority { get; set; }

    /// <summary>
    /// FK to list_options.option_id for list_id address-types
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// FK to list_options.option_id for list_id address-uses
    /// </summary>
    public string? Use { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// A=active,I=inactive
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Y=yes,N=no
    /// </summary>
    public string? IsPrimary { get; set; }

    /// <summary>
    /// Date the address became active
    /// </summary>
    public DateTime? PeriodStart { get; set; }

    /// <summary>
    /// Date the address became deactivated
    /// </summary>
    public DateTime? PeriodEnd { get; set; }

    /// <summary>
    /// [Values: Moved, Mail Returned, etc]
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
