using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class CcdaSection
{
    public int CcdaSectionsId { get; set; }

    public int? CcdaComponentsId { get; set; }

    public string? CcdaSectionsField { get; set; }

    public string? CcdaSectionsName { get; set; }

    public sbyte CcdaSectionsReqMapping { get; set; }
}
