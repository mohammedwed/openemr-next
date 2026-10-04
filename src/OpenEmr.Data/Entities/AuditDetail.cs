using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class AuditDetail
{
    public long Id { get; set; }

    /// <summary>
    /// openemr table name
    /// </summary>
    public string TableName { get; set; } = null!;

    /// <summary>
    /// openemr table&apos;s field name
    /// </summary>
    public string FieldName { get; set; } = null!;

    /// <summary>
    /// openemr table&apos;s field value
    /// </summary>
    public string? FieldValue { get; set; }

    /// <summary>
    /// Id of the audit_master table
    /// </summary>
    public long AuditMasterId { get; set; }

    /// <summary>
    /// Used when multiple entry occurs from the same table.1 means no multiple entry
    /// </summary>
    public string EntryIdentification { get; set; } = null!;
}
