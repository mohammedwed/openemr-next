using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class OnsiteMail
{
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    public string? Owner { get; set; }

    public string? User { get; set; }

    public string? Groupname { get; set; }

    public sbyte? Activity { get; set; }

    public sbyte? Authorized { get; set; }

    public string? Header { get; set; }

    public string? Title { get; set; }

    public string? Body { get; set; }

    public string? RecipientId { get; set; }

    public string? RecipientName { get; set; }

    public string? SenderId { get; set; }

    public string? SenderName { get; set; }

    public string? AssignedTo { get; set; }

    /// <summary>
    /// flag indicates note is deleted
    /// </summary>
    public sbyte? Deleted { get; set; }

    public DateTime? DeleteDate { get; set; }

    public string? Mtype { get; set; }

    public string MessageStatus { get; set; } = null!;

    public int? MailChain { get; set; }

    public int? ReplyMailChain { get; set; }

    /// <summary>
    /// Whether messsage encrypted 0-Not encrypted, 1-Encrypted
    /// </summary>
    public sbyte? IsMsgEncrypted { get; set; }
}
