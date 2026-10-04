using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class CareTeam
{
    public int Id { get; set; }

    public byte[]? Uuid { get; set; }

    /// <summary>
    /// fk to patient_data.pid
    /// </summary>
    public int Pid { get; set; }

    /// <summary>
    /// fk to list_options.option_id where list_id=Care_Team_Status
    /// </summary>
    public string? Status { get; set; }

    public string? TeamName { get; set; }

    public string? Note { get; set; }

    public DateTime? DateCreated { get; set; }

    public DateTime? DateUpdated { get; set; }

    /// <summary>
    /// fk to users.id for user who created this record
    /// </summary>
    public long? CreatedBy { get; set; }

    /// <summary>
    /// fk to users.id for user who last updated this record
    /// </summary>
    public long? UpdatedBy { get; set; }
}
