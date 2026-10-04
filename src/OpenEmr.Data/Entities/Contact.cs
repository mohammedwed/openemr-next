using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Contact
{
    public long Id { get; set; }

    public string ForeignTableName { get; set; } = null!;

    public long ForeignId { get; set; }
}
