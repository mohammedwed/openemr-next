using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class CcdaFieldMapping
{
    public int Id { get; set; }

    public int? TableId { get; set; }

    public string? CcdaField { get; set; }
}
