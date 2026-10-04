using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class LoginMfaRegistration
{
    public long UserId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>
    /// Timestamp of the last successful TOTP verification.
    /// </summary>
    public DateTime? LastChallenge { get; set; }

    /// <summary>
    /// TOTP time slice (RFC 6238) of the last consumed code. Incoming codes must land on a strictly greater slice; guards against A-B-A replay across two adjacent valid codes within the 90s acceptance window.
    /// </summary>
    public long? LastUsedStep { get; set; }

    /// <summary>
    /// Q&amp;A, U2F, TOTP etc.
    /// </summary>
    public string Method { get; set; } = null!;

    /// <summary>
    /// Question, U2F registration etc.
    /// </summary>
    public string Var1 { get; set; } = null!;

    /// <summary>
    /// Answer etc.
    /// </summary>
    public string Var2 { get; set; } = null!;
}
