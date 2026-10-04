using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class AutomaticNotification
{
    public int NotificationId { get; set; }

    public string SmsGatewayType { get; set; } = null!;

    public string ProviderName { get; set; } = null!;

    public string? Message { get; set; }

    public string EmailSender { get; set; } = null!;

    public string EmailSubject { get; set; } = null!;

    public string Type { get; set; } = null!;
}
