using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Transaction
{
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    public string Title { get; set; } = null!;

    public long? Pid { get; set; }

    public string User { get; set; } = null!;

    public string Groupname { get; set; } = null!;

    public sbyte? Authorized { get; set; }
}
