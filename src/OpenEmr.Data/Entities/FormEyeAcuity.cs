using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormEyeAcuity
{
    /// <summary>
    /// Links to forms.form_id
    /// </summary>
    public long Id { get; set; }

    public long? Pid { get; set; }

    public string? Scodva { get; set; }

    public string? Scosva { get; set; }

    public string? Phodva { get; set; }

    public string? Phosva { get; set; }

    public string? Ctlodva { get; set; }

    public string? Ctlosva { get; set; }

    public string? Mrodva { get; set; }

    public string? Mrosva { get; set; }

    public string? Scnearodva { get; set; }

    public string? Scnearosva { get; set; }

    public string? Mrnearodva { get; set; }

    public string? Mrnearosva { get; set; }

    public string? Glareodva { get; set; }

    public string? Glareosva { get; set; }

    public string? Glarecomments { get; set; }

    public string? Arodva { get; set; }

    public string? Arosva { get; set; }

    public string? Crodva { get; set; }

    public string? Crosva { get; set; }

    public string? Ctlodva1 { get; set; }

    public string? Ctlosva1 { get; set; }

    public string? Pamodva { get; set; }

    public string? Pamosva { get; set; }

    public string Liodva { get; set; } = null!;

    public string Liosva { get; set; } = null!;

    public string? Wodvanear { get; set; }

    public string? Osvanearcc { get; set; }

    public string? Binocva { get; set; }
}
