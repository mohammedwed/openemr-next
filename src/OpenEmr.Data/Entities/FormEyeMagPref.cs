using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormEyeMagPref
{
    public string? Pezone { get; set; }

    public string? Location { get; set; }

    public string LocationText { get; set; } = null!;

    public long? Id { get; set; }

    public string? Selection { get; set; }

    public int? ZoneOrder { get; set; }

    public string? Govalue { get; set; }

    public short? Ordering { get; set; }

    public string FillAction { get; set; } = null!;

    public string Goright { get; set; } = null!;

    public string Goleft { get; set; } = null!;

    public string Unspec { get; set; } = null!;
}
