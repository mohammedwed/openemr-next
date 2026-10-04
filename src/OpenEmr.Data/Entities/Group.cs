using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Group
{
    public long Id { get; set; }

    public string? Name { get; set; }

    public string? User { get; set; }
}
