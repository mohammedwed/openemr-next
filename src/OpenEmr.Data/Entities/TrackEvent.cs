using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Telemetry Event Data
/// </summary>
public partial class TrackEvent
{
    public uint Id { get; set; }

    public string? EventType { get; set; }

    public string? EventLabel { get; set; }

    public string? EventUrl { get; set; }

    public string? EventTarget { get; set; }

    public DateTime? FirstEvent { get; set; }

    public DateTime? LastEvent { get; set; }

    public uint LabelCount { get; set; }
}
