using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ModuleAclUserSetting
{
    public int ModuleId { get; set; }

    public int UserId { get; set; }

    public int SectionId { get; set; }

    public int? Allowed { get; set; }
}
