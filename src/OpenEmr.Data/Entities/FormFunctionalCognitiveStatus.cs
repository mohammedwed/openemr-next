using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormFunctionalCognitiveStatus
{
    public long Id { get; set; }

    public DateOnly? Date { get; set; }

    public long? Pid { get; set; }

    public string? Encounter { get; set; }

    public string? User { get; set; }

    public string? Groupname { get; set; }

    public sbyte? Authorized { get; set; }

    public sbyte? Activity { get; set; }

    public string? Code { get; set; }

    public string? Codetext { get; set; }

    public string? Description { get; set; }

    public string? ExternalId { get; set; }
}
