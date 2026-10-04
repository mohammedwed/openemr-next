using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Note
{
    public int Id { get; set; }

    public int ForeignId { get; set; }

    public string? Note1 { get; set; }

    public int? Owner { get; set; }

    public DateTime? Date { get; set; }

    public DateTime Revision { get; set; }
}
