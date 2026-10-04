using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormTaskman
{
    public long Id { get; set; }

    public DateTime ReqDate { get; set; }

    public long FromId { get; set; }

    public long ToId { get; set; }

    public long PatientId { get; set; }

    public string? DocType { get; set; }

    public long? DocId { get; set; }

    public long? EncId { get; set; }

    public string Method { get; set; } = null!;

    /// <summary>
    /// 1 = completed
    /// </summary>
    public string? Completed { get; set; }

    public DateTime? CompletedDate { get; set; }

    public string? Comment { get; set; }

    public string? Userfield1 { get; set; }
}
