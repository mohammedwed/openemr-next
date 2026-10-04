using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ModuleAclGroupSetting
{
    public int ModuleId { get; set; }

    public int GroupId { get; set; }

    public int SectionId { get; set; }

    public bool? Allowed { get; set; }
}
