using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Payment
{
    public long Id { get; set; }

    public long Pid { get; set; }

    public DateTime Dtime { get; set; }

    public long Encounter { get; set; }

    public string? User { get; set; }

    public string? Method { get; set; }

    public string? Source { get; set; }

    public decimal Amount1 { get; set; }

    public decimal Amount2 { get; set; }

    public decimal Posted1 { get; set; }

    public decimal Posted2 { get; set; }
}
