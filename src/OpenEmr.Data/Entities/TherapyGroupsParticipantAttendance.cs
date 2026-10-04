using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class TherapyGroupsParticipantAttendance
{
    public int FormId { get; set; }

    public long Pid { get; set; }

    public string? MeetingPatientComment { get; set; }

    public string? MeetingPatientStatus { get; set; }
}
