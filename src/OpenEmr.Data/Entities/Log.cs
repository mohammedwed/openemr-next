using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Log
{
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    public string? Event { get; set; }

    public string? Category { get; set; }

    public string? User { get; set; }

    public string? Groupname { get; set; }

    public string? Comments { get; set; }

    public string? UserNotes { get; set; }

    public long? PatientId { get; set; }

    public bool? Success { get; set; }

    public string? Checksum { get; set; }

    public string? CrtUser { get; set; }

    public string? LogFrom { get; set; }

    public int? MenuItemId { get; set; }

    /// <summary>
    /// CCDA document id from ccda
    /// </summary>
    public int? CcdaDocId { get; set; }
}
