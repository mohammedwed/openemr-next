using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class RecentPatient
{
    public string UserId { get; set; } = null!;

    public string? Patients { get; set; }
}
