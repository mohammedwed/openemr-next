using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class StandardizedTablesTrack
{
    public int Id { get; set; }

    public DateTime? ImportedDate { get; set; }

    /// <summary>
    /// name of standardized tables such as RXNORM
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// revision of standardized tables that were imported
    /// </summary>
    public string RevisionVersion { get; set; } = null!;

    /// <summary>
    /// revision of standardized tables that were imported
    /// </summary>
    public DateTime? RevisionDate { get; set; }

    public string FileChecksum { get; set; } = null!;
}
