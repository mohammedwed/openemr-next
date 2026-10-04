using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class IssueEncounter
{
    public long Id { get; set; }

    /// <summary>
    /// UUID for this issue encounter record, for data exchange purposes
    /// </summary>
    public byte[]? Uuid { get; set; }

    public long Pid { get; set; }

    public int ListId { get; set; }

    public int Encounter { get; set; }

    public bool Resolved { get; set; }

    /// <summary>
    /// fk to users.id for the user that entered in the issue encounter data
    /// </summary>
    public long? CreatedBy { get; set; }

    /// <summary>
    /// fk to users.id for the user that last updated the issue encounter data
    /// </summary>
    public long? UpdatedBy { get; set; }

    /// <summary>
    /// timestamp when this issue encounter record was created
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// timestamp when this issue encounter record was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
