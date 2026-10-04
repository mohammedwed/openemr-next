using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class CcdaTableMapping
{
    public int Id { get; set; }

    public string? CcdaComponent { get; set; }

    public string? CcdaComponentSection { get; set; }

    public string? FormDir { get; set; }

    public short? FormType { get; set; }

    public string? FormTable { get; set; }

    public int? UserId { get; set; }

    public sbyte Deleted { get; set; }

    public DateTime Timestamp { get; set; }
}
