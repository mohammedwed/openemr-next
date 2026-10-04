using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class PatientAccessOnsite
{
    public int Id { get; set; }

    public long? Pid { get; set; }

    public string? PortalUsername { get; set; }

    public string? PortalPwd { get; set; }

    /// <summary>
    /// 0=&gt;Password Created Through Demographics by The provider or staff. Patient Should Change it at first time it.1=&gt;Pwd updated or created by patient itself
    /// </summary>
    public sbyte? PortalPwdStatus { get; set; }

    /// <summary>
    /// User entered username
    /// </summary>
    public string? PortalLoginUsername { get; set; }

    public string? PortalOnetime { get; set; }

    public DateTime DateCreated { get; set; }

    /// <summary>
    /// Per-portal-account failure counter. Independent of ip_login_fail_counter so a valid login on account A cannot clear an in-progress brute force against account B.
    /// </summary>
    public long? PortalFailCounter { get; set; }

    /// <summary>
    /// Timestamp of the last portal login failure for this account. Used for time-based counter reset.
    /// </summary>
    public DateTime? PortalLastFail { get; set; }
}
