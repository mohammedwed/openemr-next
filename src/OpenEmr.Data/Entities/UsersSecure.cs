using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class UsersSecure
{
    public long Id { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public DateTime? LastUpdatePassword { get; set; }

    public DateTime LastUpdate { get; set; }

    public string? PasswordHistory1 { get; set; }

    public string? PasswordHistory2 { get; set; }

    public string? PasswordHistory3 { get; set; }

    public string? PasswordHistory4 { get; set; }

    public DateTime? LastChallengeResponse { get; set; }

    public string? LoginWorkArea { get; set; }

    public long? TotalLoginFailCounter { get; set; }

    public int? LoginFailCounter { get; set; }

    public DateTime? LastLoginFail { get; set; }

    public sbyte? AutoBlockEmailed { get; set; }

    /// <summary>
    /// Per-user MFA challenge failure counter. Independent of login_fail_counter so an in-progress MFA brute force does not get zeroed out by the password verify success that happens on every attempt.
    /// </summary>
    public long? MfaFailCounter { get; set; }

    /// <summary>
    /// Timestamp of the last MFA challenge failure. Used for time-based counter reset.
    /// </summary>
    public DateTime? MfaLastFail { get; set; }
}
