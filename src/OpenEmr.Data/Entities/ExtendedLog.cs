using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ExtendedLog
{
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    public string? Event { get; set; }

    public string? User { get; set; }

    public string? Recipient { get; set; }

    public string? Description { get; set; }

    public long? PatientId { get; set; }
}
