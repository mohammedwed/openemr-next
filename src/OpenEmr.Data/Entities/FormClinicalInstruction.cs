using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormClinicalInstruction
{
    public int Id { get; set; }

    public long? Pid { get; set; }

    public string? Encounter { get; set; }

    public string? User { get; set; }

    public string? Instruction { get; set; }

    public DateTime Date { get; set; }

    public sbyte? Activity { get; set; }
}
