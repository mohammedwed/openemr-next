using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ModulesSetting
{
    public int? ModId { get; set; }

    /// <summary>
    /// 1=&gt;ACL,2=&gt;preferences,3=&gt;hooks
    /// </summary>
    public short? FldType { get; set; }

    public string? ObjName { get; set; }

    public string? MenuName { get; set; }

    public string? Path { get; set; }
}
