using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class CcdaComponent
{
    public int CcdaComponentsId { get; set; }

    public string? CcdaComponentsField { get; set; }

    public string? CcdaComponentsName { get; set; }

    /// <summary>
    /// 0=&gt;sections,1=&gt;components
    /// </summary>
    public int CcdaType { get; set; }
}
