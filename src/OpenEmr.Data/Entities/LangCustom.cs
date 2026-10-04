using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class LangCustom
{
    public string LangDescription { get; set; } = null!;

    public string LangCode { get; set; } = null!;

    public string? ConstantName { get; set; }

    public string? Definition { get; set; }
}
