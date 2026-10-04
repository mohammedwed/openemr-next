using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class DatedRemindersLink
{
    public int DrLinkId { get; set; }

    public int DrId { get; set; }

    public int ToId { get; set; }
}
