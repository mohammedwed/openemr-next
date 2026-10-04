using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ValuesetOid
{
    public string NqfCode { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string CodeSystem { get; set; } = null!;

    public string? CodeType { get; set; }

    public string Valueset { get; set; } = null!;

    public string? Description { get; set; }

    public string? ValuesetName { get; set; }
}
