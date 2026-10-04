using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ModuleAclSection
{
    public int? SectionId { get; set; }

    public string? SectionName { get; set; }

    public int? ParentSection { get; set; }

    public string? SectionIdentifier { get; set; }

    public int? ModuleId { get; set; }
}
