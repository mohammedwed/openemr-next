using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ModuleConfiguration
{
    public uint ModuleConfigId { get; set; }

    public uint ModuleId { get; set; }

    public string FieldName { get; set; } = null!;

    public string FieldValue { get; set; } = null!;

    /// <summary>
    /// users.id the user that first created this record
    /// </summary>
    public long? CreatedBy { get; set; }

    /// <summary>
    /// Datetime the record was initially created
    /// </summary>
    public DateTime? DateAdded { get; set; }

    /// <summary>
    /// users.id the user that last modified this record
    /// </summary>
    public long? UpdatedBy { get; set; }

    /// <summary>
    /// Datetime the record was last modified
    /// </summary>
    public DateTime? DateModified { get; set; }

    /// <summary>
    /// Datetime the record was created
    /// </summary>
    public DateTime? DateCreated { get; set; }
}
