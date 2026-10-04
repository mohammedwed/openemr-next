using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class PatientPortalMenu
{
    public int PatientPortalMenuId { get; set; }

    public int? PatientPortalMenuGroupId { get; set; }

    public string? MenuName { get; set; }

    public short? MenuOrder { get; set; }

    public sbyte? MenuStatus { get; set; }
}
