using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormGroupAttendance
{
    public long Id { get; set; }

    public DateOnly? Date { get; set; }

    public int? GroupId { get; set; }

    public string? User { get; set; }

    public string? Groupname { get; set; }

    public sbyte? Authorized { get; set; }

    public int? EncounterId { get; set; }

    public sbyte? Activity { get; set; }
}
