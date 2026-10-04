using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class DatedReminder
{
    public int DrId { get; set; }

    public int DrFromId { get; set; }

    public string DrMessageText { get; set; } = null!;

    public DateTime DrMessageSentDate { get; set; }

    public DateOnly DrMessageDueDate { get; set; }

    public long Pid { get; set; }

    public bool MessagePriority { get; set; }

    public bool MessageProcessed { get; set; }

    public DateTime? ProcessedDate { get; set; }

    public int DrProcessedBy { get; set; }
}
