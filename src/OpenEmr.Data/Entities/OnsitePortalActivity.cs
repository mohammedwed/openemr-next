using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class OnsitePortalActivity
{
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    public long? PatientId { get; set; }

    public string? Activity { get; set; }

    public bool? RequireAudit { get; set; }

    public string? PendingAction { get; set; }

    public string? ActionTaken { get; set; }

    public string? Status { get; set; }

    public string? Narrative { get; set; }

    public string? TableAction { get; set; }

    public string? TableArgs { get; set; }

    public int? ActionUser { get; set; }

    public DateTime? ActionTakenTime { get; set; }

    public string? Checksum { get; set; }
}
