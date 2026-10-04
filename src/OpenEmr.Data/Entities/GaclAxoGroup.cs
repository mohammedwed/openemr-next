using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class GaclAxoGroup
{
    public int Id { get; set; }

    public int ParentId { get; set; }

    public int Lft { get; set; }

    public int Rgt { get; set; }

    public string Name { get; set; } = null!;

    public string Value { get; set; } = null!;
}
