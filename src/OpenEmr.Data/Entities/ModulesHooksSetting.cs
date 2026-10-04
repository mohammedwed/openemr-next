using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ModulesHooksSetting
{
    public int Id { get; set; }

    public int? ModId { get; set; }

    public string? EnabledHooks { get; set; }

    public string? AttachedTo { get; set; }
}
