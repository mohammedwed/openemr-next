using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class UuidRegistry
{
    public byte[] Uuid { get; set; } = null!;

    public string TableName { get; set; } = null!;

    public string TableId { get; set; } = null!;

    public string TableVertical { get; set; } = null!;

    public string Couchdb { get; set; } = null!;

    public sbyte DocumentDrive { get; set; }

    public sbyte Mapped { get; set; }

    public DateTime? Created { get; set; }
}
