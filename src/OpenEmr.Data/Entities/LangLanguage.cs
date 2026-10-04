using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class LangLanguage
{
    public int LangId { get; set; }

    public string LangCode { get; set; } = null!;

    public string? LangDescription { get; set; }

    public sbyte? LangIsRtl { get; set; }
}
