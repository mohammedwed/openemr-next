using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class PatientBirthdayAlert
{
    public long Pid { get; set; }

    public long UserId { get; set; }

    public DateOnly TurnedOffOn { get; set; }
}
