using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Onote
{
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    public string? Body { get; set; }

    public string? User { get; set; }

    public string? Groupname { get; set; }

    public sbyte? Activity { get; set; }
}
