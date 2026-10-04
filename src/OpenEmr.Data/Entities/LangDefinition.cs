using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class LangDefinition
{
    public int DefId { get; set; }

    public int ConsId { get; set; }

    public int LangId { get; set; }

    public string? Definition { get; set; }
}
