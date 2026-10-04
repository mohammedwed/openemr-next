using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Global
{
    public string GlName { get; set; } = null!;

    public int GlIndex { get; set; }

    public string GlValue { get; set; } = null!;
}
