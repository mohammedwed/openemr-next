using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class TherapyGroupsParticipant
{
    public int GroupId { get; set; }

    public long Pid { get; set; }

    public int GroupPatientStatus { get; set; }

    public DateOnly GroupPatientStart { get; set; }

    public DateOnly? GroupPatientEnd { get; set; }

    public string? GroupPatientComment { get; set; }
}
