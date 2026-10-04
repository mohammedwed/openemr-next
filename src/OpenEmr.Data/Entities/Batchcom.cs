using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Batchcom
{
    public long Id { get; set; }

    public long PatientId { get; set; }

    public long SentBy { get; set; }

    public string? MsgType { get; set; }

    public string? MsgSubject { get; set; }

    public string? MsgText { get; set; }

    public DateTime? MsgDateSent { get; set; }
}
