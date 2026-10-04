using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Key
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Value { get; set; }
}
