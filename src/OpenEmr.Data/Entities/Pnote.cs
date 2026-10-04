using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Pnote
{
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    public string? Body { get; set; }

    public long? Pid { get; set; }

    public string? User { get; set; }

    public string? Groupname { get; set; }

    public sbyte? Activity { get; set; }

    public sbyte? Authorized { get; set; }

    public string? Title { get; set; }

    public string? AssignedTo { get; set; }

    /// <summary>
    /// flag indicates note is deleted
    /// </summary>
    public sbyte? Deleted { get; set; }

    public string MessageStatus { get; set; } = null!;

    public string? PortalRelation { get; set; }

    /// <summary>
    /// Whether messsage encrypted 0-Not encrypted, 1-Encrypted
    /// </summary>
    public sbyte? IsMsgEncrypted { get; set; }

    public long? UpdateBy { get; set; }

    public DateTime? UpdateDate { get; set; }
}
