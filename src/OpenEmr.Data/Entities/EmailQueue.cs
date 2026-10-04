using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class EmailQueue
{
    public long Id { get; set; }

    public string? Sender { get; set; }

    public string? Recipient { get; set; }

    public string? Subject { get; set; }

    public string? Body { get; set; }

    public DateTime? DatetimeQueued { get; set; }

    public sbyte? Sent { get; set; }

    public DateTime? DatetimeSent { get; set; }

    public sbyte? Error { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? DatetimeError { get; set; }

    /// <summary>
    /// The folder prefix and base filename (w/o extension) of the twig template file to use for this email
    /// </summary>
    public string? TemplateName { get; set; }
}
