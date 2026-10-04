using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormEyeRo
{
    /// <summary>
    /// Links to forms.form_id
    /// </summary>
    public long Id { get; set; }

    public long? Pid { get; set; }

    public string? Rosgeneral { get; set; }

    public string? Rosheent { get; set; }

    public string? Roscv { get; set; }

    public string? Rospulm { get; set; }

    public string? Rosgi { get; set; }

    public string? Rosgu { get; set; }

    public string? Rosderm { get; set; }

    public string? Rosneuro { get; set; }

    public string? Rospsych { get; set; }

    public string? Rosmusculo { get; set; }

    public string? Rosimmuno { get; set; }

    public string? Rosendocrine { get; set; }

    public string? Roscomments { get; set; }
}
