using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class MedexIcon
{
    public int IUid { get; set; }

    public string MsgType { get; set; } = null!;

    public string MsgStatus { get; set; } = null!;

    public string? IDescription { get; set; }

    public string? IHtml { get; set; }

    public string? IBlob { get; set; }
}
