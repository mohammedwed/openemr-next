using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormEyeVital
{
    /// <summary>
    /// Links to forms.form_id
    /// </summary>
    public long Id { get; set; }

    public long? Pid { get; set; }

    public string? Alert { get; set; }

    public string? Oriented { get; set; }

    public string? Confused { get; set; }

    public string? Odiopap { get; set; }

    public string? Osiopap { get; set; }

    public string? Odioptpn { get; set; }

    public string? Osioptpn { get; set; }

    public string? Odiopftn { get; set; }

    public string? Osiopftn { get; set; }

    public TimeOnly Ioptime { get; set; }

    public string Odioppost { get; set; } = null!;

    public string Osioppost { get; set; } = null!;

    public TimeOnly? Iopposttime { get; set; }

    public string Odioptarget { get; set; } = null!;

    public string Osioptarget { get; set; } = null!;

    public short? Amslerod { get; set; }

    public short? Amsleros { get; set; }

    public bool? Odvf1 { get; set; }

    public bool? Odvf2 { get; set; }

    public bool? Odvf3 { get; set; }

    public bool? Odvf4 { get; set; }

    public bool? Osvf1 { get; set; }

    public bool? Osvf2 { get; set; }

    public bool? Osvf3 { get; set; }

    public bool? Osvf4 { get; set; }
}
