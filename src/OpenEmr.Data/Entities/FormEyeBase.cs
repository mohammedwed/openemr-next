using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormEyeBase
{
    /// <summary>
    /// Links to forms.form_id
    /// </summary>
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    public long? Pid { get; set; }

    public string? User { get; set; }

    public string? Groupname { get; set; }

    public sbyte? Authorized { get; set; }

    public sbyte? Activity { get; set; }
}
