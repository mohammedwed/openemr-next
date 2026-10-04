using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class DirectMessageLog
{
    public long Id { get; set; }

    /// <summary>
    /// S=sent,R=received
    /// </summary>
    public string MsgType { get; set; } = null!;

    public string MsgId { get; set; } = null!;

    public string Sender { get; set; } = null!;

    public string Recipient { get; set; } = null!;

    public DateTime CreateTs { get; set; }

    /// <summary>
    /// Q=queued,D=dispatched,R=received,F=failed
    /// </summary>
    public string Status { get; set; } = null!;

    public string? StatusInfo { get; set; }

    public DateTime? StatusTs { get; set; }

    public long? PatientId { get; set; }

    public long? UserId { get; set; }
}
