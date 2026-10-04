using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class DocumentTemplate
{
    public ulong Id { get; set; }

    public long? Pid { get; set; }

    public uint? Provider { get; set; }

    public uint? Encounter { get; set; }

    public DateTime ModifiedDate { get; set; }

    public string Profile { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string? Location { get; set; }

    public string? TemplateName { get; set; }

    public string? Status { get; set; }

    public DateTime SendDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int Size { get; set; }

    public byte[]? TemplateContent { get; set; }

    public string? Mime { get; set; }
}
