using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class NotificationSetting
{
    public int SettingsId { get; set; }

    public int SendSmsBeforeHours { get; set; }

    public int SendEmailBeforeHours { get; set; }

    public string SmsGatewayUsername { get; set; } = null!;

    public string SmsGatewayPassword { get; set; } = null!;

    public string SmsGatewayApikey { get; set; } = null!;

    public string Type { get; set; } = null!;
}
