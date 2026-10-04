using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormEyeExternal
{
    /// <summary>
    /// Links to forms.form_id
    /// </summary>
    public long Id { get; set; }

    public long? Pid { get; set; }

    public string? Rul { get; set; }

    public string? Lul { get; set; }

    public string? Rll { get; set; }

    public string? Lll { get; set; }

    public string? Rbrow { get; set; }

    public string? Lbrow { get; set; }

    public string? Rmct { get; set; }

    public string? Lmct { get; set; }

    public string? Radnexa { get; set; }

    public string? Ladnexa { get; set; }

    public string? Rmrd { get; set; }

    public string? Lmrd { get; set; }

    public string? Rlf { get; set; }

    public string? Llf { get; set; }

    public string? Rvfissure { get; set; }

    public string? Lvfissure { get; set; }

    public string? Odhertel { get; set; }

    public string? Oshertel { get; set; }

    public string? Hertelbase { get; set; }

    public string? Rcarotid { get; set; }

    public string? Lcarotid { get; set; }

    public string? Rtempart { get; set; }

    public string? Ltempart { get; set; }

    public string? Rcnv { get; set; }

    public string? Lcnv { get; set; }

    public string? Rcnvii { get; set; }

    public string? Lcnvii { get; set; }

    public string? ExtComments { get; set; }
}
