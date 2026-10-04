using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormEyeBiometric
{
    /// <summary>
    /// Links to forms.form_id
    /// </summary>
    public long Id { get; set; }

    public long? Pid { get; set; }

    public string? Odk1 { get; set; }

    public string? Odk2 { get; set; }

    public string? Odk2axis { get; set; }

    public string? Osk1 { get; set; }

    public string? Osk2 { get; set; }

    public string? Osk2axis { get; set; }

    public string? Odaxiallength { get; set; }

    public string? Osaxiallength { get; set; }

    public string? Odpdmeasured { get; set; }

    public string? Ospdmeasured { get; set; }

    public string? Odacd { get; set; }

    public string? Osacd { get; set; }

    public string? Odw2w { get; set; }

    public string? Osw2w { get; set; }

    public string? Odlt { get; set; }

    public string? Oslt { get; set; }
}
