using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormEyeMagOrder
{
    public long Id { get; set; }

    public int FormId { get; set; }

    public long Pid { get; set; }

    public string OrderDetails { get; set; } = null!;

    public string? OrderStatus { get; set; }

    public string? OrderPriority { get; set; }

    public DateOnly OrderDatePlaced { get; set; }

    public string? OrderPlacedBywhom { get; set; }

    public DateOnly? OrderDateCompleted { get; set; }

    public string? OrderCompletedBywhom { get; set; }
}
