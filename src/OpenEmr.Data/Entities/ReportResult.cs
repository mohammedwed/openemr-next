using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ReportResult
{
    public long ReportId { get; set; }

    public string FieldId { get; set; } = null!;

    public string? FieldValue { get; set; }
}
