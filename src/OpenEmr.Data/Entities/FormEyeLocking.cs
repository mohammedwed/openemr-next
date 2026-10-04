using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormEyeLocking
{
    /// <summary>
    /// Links to forms.form_id
    /// </summary>
    public long Id { get; set; }

    public long? Pid { get; set; }

    public string? Imp { get; set; }

    public string? Plan { get; set; }

    public string? Resource { get; set; }

    public string? Technician { get; set; }

    public string? Locked { get; set; }

    public DateTime Lockeddate { get; set; }

    public string? Lockedby { get; set; }
}
