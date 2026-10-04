using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class NotificationLog
{
    public int ILogId { get; set; }

    public long Pid { get; set; }

    public uint? PcEid { get; set; }

    public string SmsGatewayType { get; set; } = null!;

    public string SmsgatewayInfo { get; set; } = null!;

    public string? Message { get; set; }

    public string EmailSender { get; set; } = null!;

    public string EmailSubject { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string? PatientInfo { get; set; }

    public DateOnly PcEventDate { get; set; }

    public DateOnly PcEndDate { get; set; }

    public TimeOnly PcStartTime { get; set; }

    public TimeOnly PcEndTime { get; set; }

    public DateTime DSentDateTime { get; set; }
}
