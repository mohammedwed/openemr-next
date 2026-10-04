using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class CalendarExternal
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public string Description { get; set; } = null!;

    public string? Source { get; set; }
}
