using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class MedexOutgoing
{
    public int MsgUid { get; set; }

    public int MsgPid { get; set; }

    public string MsgPcEid { get; set; } = null!;

    public int CampaignUid { get; set; }

    public DateTime MsgDate { get; set; }

    public string MsgType { get; set; } = null!;

    public string? MsgReply { get; set; }

    public string? MsgExtraText { get; set; }

    public int? MedexUid { get; set; }
}
