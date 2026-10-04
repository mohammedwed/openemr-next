using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class SyndromicSurveillance
{
    public long Id { get; set; }

    public long ListsId { get; set; }

    public DateTime SubmissionDate { get; set; }

    public string Filename { get; set; } = null!;
}
