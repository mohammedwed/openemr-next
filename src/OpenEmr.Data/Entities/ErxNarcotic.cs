using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ErxNarcotic
{
    public int Id { get; set; }

    public string Drug { get; set; } = null!;

    public string DeaNumber { get; set; } = null!;

    public string CsaSch { get; set; } = null!;

    public string Narc { get; set; } = null!;

    public string OtherNames { get; set; } = null!;
}
