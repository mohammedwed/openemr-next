using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class PatientSetting
{
    public long SettingPatient { get; set; }

    public string SettingLabel { get; set; } = null!;

    public string SettingValue { get; set; } = null!;
}
