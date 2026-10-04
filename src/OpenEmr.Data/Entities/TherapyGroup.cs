using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class TherapyGroup
{
    public int GroupId { get; set; }

    public string GroupName { get; set; } = null!;

    public DateOnly GroupStartDate { get; set; }

    public DateOnly? GroupEndDate { get; set; }

    public sbyte GroupType { get; set; }

    public sbyte GroupParticipation { get; set; }

    public int GroupStatus { get; set; }

    public string? GroupNotes { get; set; }

    public string? GroupGuestCounselors { get; set; }
}
