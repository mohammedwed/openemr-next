using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class List
{
    public long Id { get; set; }

    public byte[]? Uuid { get; set; }

    public DateTime? Date { get; set; }

    public string? Type { get; set; }

    public string Subtype { get; set; } = null!;

    public string? Title { get; set; }

    public string? Udi { get; set; }

    public string? UdiData { get; set; }

    public DateTime? Begdate { get; set; }

    public DateTime? Enddate { get; set; }

    public DateOnly? Returndate { get; set; }

    /// <summary>
    /// Reference to list_options option_id=&apos;occurrence&apos;
    /// </summary>
    public int? Occurrence { get; set; }

    public int? Classification { get; set; }

    public string? Referredby { get; set; }

    public string? Extrainfo { get; set; }

    public string? Diagnosis { get; set; }

    public sbyte? Activity { get; set; }

    public string? Comments { get; set; }

    public long? Pid { get; set; }

    public string? User { get; set; }

    public string? Groupname { get; set; }

    public int Outcome { get; set; }

    public string? Destination { get; set; }

    public long ReinjuryId { get; set; }

    public string InjuryPart { get; set; } = null!;

    public string InjuryType { get; set; } = null!;

    public string InjuryGrade { get; set; } = null!;

    public string Reaction { get; set; } = null!;

    /// <summary>
    /// Reference to list_options option_id = allergyintolerance-verification
    /// </summary>
    public string Verification { get; set; } = null!;

    public int? ExternalAllergyid { get; set; }

    /// <summary>
    /// 0-OpenEMR 1-External
    /// </summary>
    public string ErxSource { get; set; } = null!;

    /// <summary>
    /// 0-Pending NewCrop upload 1-Uploaded TO NewCrop
    /// </summary>
    public string ErxUploaded { get; set; } = null!;

    public DateTime Modifydate { get; set; }

    public string? SeverityAl { get; set; }

    public string? ExternalId { get; set; }

    /// <summary>
    /// Reference to list_options table
    /// </summary>
    public string? ListOptionId { get; set; }
}
