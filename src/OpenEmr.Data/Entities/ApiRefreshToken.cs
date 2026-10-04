using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Holds information about api refresh tokens.
/// </summary>
public partial class ApiRefreshToken
{
    public long Id { get; set; }

    public string? UserId { get; set; }

    public string? ClientId { get; set; }

    public string Token { get; set; } = null!;

    public DateTime? Expiry { get; set; }

    /// <summary>
    /// 1=revoked,0=not revoked
    /// </summary>
    public bool Revoked { get; set; }
}
