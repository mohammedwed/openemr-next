using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormEyePostseg
{
    /// <summary>
    /// Links to forms.form_id
    /// </summary>
    public long Id { get; set; }

    public long? Pid { get; set; }

    public string? Oddisc { get; set; }

    public string? Osdisc { get; set; }

    public string? Odcup { get; set; }

    public string? Oscup { get; set; }

    public string? Odmacula { get; set; }

    public string? Osmacula { get; set; }

    public string? Odvessels { get; set; }

    public string? Osvessels { get; set; }

    public string? Odvitreous { get; set; }

    public string? Osvitreous { get; set; }

    public string? Odperiph { get; set; }

    public string? Osperiph { get; set; }

    public string? Odcmt { get; set; }

    public string? Oscmt { get; set; }

    public string? RetinaComments { get; set; }

    public string DilRisks { get; set; } = null!;

    public string? DilMeds { get; set; }

    public string Wettype { get; set; } = null!;

    public string Atropine { get; set; } = null!;

    public string Cyclomydril { get; set; } = null!;

    public string Tropicamide { get; set; } = null!;

    public string Cyclogyl { get; set; } = null!;

    public string Neo25 { get; set; } = null!;
}
