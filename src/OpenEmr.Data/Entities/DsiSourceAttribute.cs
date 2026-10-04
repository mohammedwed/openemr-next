using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Holds information about decision support intervention system source attributes
/// </summary>
public partial class DsiSourceAttribute
{
    public ulong Id { get; set; }

    public string ClientId { get; set; } = null!;

    public string ListId { get; set; } = null!;

    public string OptionId { get; set; } = null!;

    public string? ClinicalRuleId { get; set; }

    public string? SourceValue { get; set; }

    public long? CreatedBy { get; set; }

    public long? LastUpdatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? LastUpdatedAt { get; set; }
}
