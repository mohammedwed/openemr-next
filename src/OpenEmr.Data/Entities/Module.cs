using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Module
{
    public int ModId { get; set; }

    public string ModName { get; set; } = null!;

    public string ModDirectory { get; set; } = null!;

    public string ModParent { get; set; } = null!;

    public string ModType { get; set; } = null!;

    public uint ModActive { get; set; }

    public string ModUiName { get; set; } = null!;

    public string ModRelativeLink { get; set; } = null!;

    public sbyte ModUiOrder { get; set; }

    public uint ModUiActive { get; set; }

    public string ModDescription { get; set; } = null!;

    public string ModNickName { get; set; } = null!;

    public string ModEncMenu { get; set; } = null!;

    public string? PermissionsItemTable { get; set; }

    public string Directory { get; set; } = null!;

    public DateTime Date { get; set; }

    public sbyte? SqlRun { get; set; }

    public sbyte? Type { get; set; }

    public string SqlVersion { get; set; } = null!;

    public string AclVersion { get; set; } = null!;
}
