using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class IpTracking
{
    public long Id { get; set; }

    public string? IpString { get; set; }

    public long? TotalIpLoginFailCounter { get; set; }

    public long? IpLoginFailCounter { get; set; }

    public DateTime? IpLastLoginFail { get; set; }

    public sbyte? IpAutoBlockEmailed { get; set; }

    public sbyte? IpForceBlock { get; set; }

    public sbyte? IpNoPreventTimingAttack { get; set; }

    /// <summary>
    /// Per-IP MFA challenge failure counter. Independent of ip_login_fail_counter so an in-progress MFA brute force is not zeroed out by the password verify success on each attempt.
    /// </summary>
    public long? MfaLoginFailCounter { get; set; }

    /// <summary>
    /// Timestamp of the last MFA challenge failure from this IP. Used for time-based counter reset.
    /// </summary>
    public DateTime? MfaLastLoginFail { get; set; }
}
