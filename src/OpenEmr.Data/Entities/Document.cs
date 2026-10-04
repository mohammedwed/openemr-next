using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Document
{
    public int Id { get; set; }

    public byte[]? Uuid { get; set; }

    public string? Type { get; set; }

    public int? Size { get; set; }

    public DateTime? Date { get; set; }

    public DateTime? DateExpires { get; set; }

    public string? Url { get; set; }

    public string? ThumbUrl { get; set; }

    public string? Mimetype { get; set; }

    public int? Pages { get; set; }

    public int? Owner { get; set; }

    public DateTime Revision { get; set; }

    public long? ForeignId { get; set; }

    public DateOnly? Docdate { get; set; }

    public string? Hash { get; set; }

    public long ListId { get; set; }

    public string? Name { get; set; }

    public byte[]? DriveUuid { get; set; }

    public string? CouchDocid { get; set; }

    public string? CouchRevid { get; set; }

    /// <summary>
    /// 0-&gt;Harddisk,1-&gt;CouchDB
    /// </summary>
    public sbyte Storagemethod { get; set; }

    /// <summary>
    /// Depth of path to use in url to find document. Not applicable for CouchDB.
    /// </summary>
    public sbyte? PathDepth { get; set; }

    /// <summary>
    /// Parsing status for CCR/CCD/CCDA importing
    /// </summary>
    public sbyte? Imported { get; set; }

    /// <summary>
    /// Encounter id if tagged
    /// </summary>
    public long EncounterId { get; set; }

    /// <summary>
    /// If encounter is created while tagging
    /// </summary>
    public bool EncounterCheck { get; set; }

    /// <summary>
    /// approval_status from audit_master table
    /// </summary>
    public sbyte AuditMasterApprovalStatus { get; set; }

    public int? AuditMasterId { get; set; }

    public string? DocumentationOf { get; set; }

    /// <summary>
    /// 0-&gt;No,1-&gt;Yes
    /// </summary>
    public sbyte Encrypted { get; set; }

    public string? DocumentData { get; set; }

    public bool Deleted { get; set; }

    public long? ForeignReferenceId { get; set; }

    public string? ForeignReferenceTable { get; set; }
}
