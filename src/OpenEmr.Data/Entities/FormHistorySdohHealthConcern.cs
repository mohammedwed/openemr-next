using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Links SDOH assessments to health concern conditions
/// </summary>
public partial class FormHistorySdohHealthConcern
{
    public long Id { get; set; }

    /// <summary>
    /// FK to form_history_sdoh.id
    /// </summary>
    public ulong SdohHistoryId { get; set; }

    /// <summary>
    /// FK to lists.id where type=health_concern or medical_problem
    /// </summary>
    public long HealthConcernId { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// FK to users.id
    /// </summary>
    public long? CreatedBy { get; set; }
}
