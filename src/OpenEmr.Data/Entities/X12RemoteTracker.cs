using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class X12RemoteTracker
{
    public long Id { get; set; }

    public int X12PartnerId { get; set; }

    public string X12Filename { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Claims { get; set; }

    public string? Messages { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
