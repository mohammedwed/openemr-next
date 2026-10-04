using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class UserSetting
{
    public long SettingUser { get; set; }

    public string SettingLabel { get; set; } = null!;

    public string SettingValue { get; set; } = null!;
}
