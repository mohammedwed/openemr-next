using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class AmcMiscDatum
{
    /// <summary>
    /// Unique and maps to list_options list clinical_rules
    /// </summary>
    public string AmcId { get; set; } = null!;

    public long? Pid { get; set; }

    /// <summary>
    /// Maps to an object category (such as prescriptions etc.)
    /// </summary>
    public string MapCategory { get; set; } = null!;

    /// <summary>
    /// Maps to an object id (such as prescription id etc.)
    /// </summary>
    public long MapId { get; set; }

    public DateTime? DateCreated { get; set; }

    public DateTime? DateCompleted { get; set; }

    public DateTime? SocProvided { get; set; }
}
