using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ErxRxLog
{
    public int Id { get; set; }

    public int PrescriptionId { get; set; }

    public string Date { get; set; } = null!;

    public string Time { get; set; } = null!;

    public int Code { get; set; }

    public string? Status { get; set; }

    public string? MessageId { get; set; }

    public int? Read { get; set; }
}
