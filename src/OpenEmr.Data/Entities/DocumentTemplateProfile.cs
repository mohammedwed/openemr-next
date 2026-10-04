using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class DocumentTemplateProfile
{
    public ulong Id { get; set; }

    public ulong TemplateId { get; set; }

    public string Profile { get; set; } = null!;

    public string TemplateName { get; set; } = null!;

    public string Category { get; set; } = null!;

    public uint? Provider { get; set; }

    public DateTime ModifiedDate { get; set; }

    public string MemberOf { get; set; } = null!;

    public bool Active { get; set; }

    public bool? Recurring { get; set; }

    public string EventTrigger { get; set; } = null!;

    public int Period { get; set; }

    public string NotifyTrigger { get; set; } = null!;

    public int NotifyPeriod { get; set; }
}
