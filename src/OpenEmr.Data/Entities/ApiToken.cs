using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ApiToken
{
    public long Id { get; set; }

    public string? UserId { get; set; }

    public string? Token { get; set; }

    public DateTime? Expiry { get; set; }

    public string? ClientId { get; set; }

    /// <summary>
    /// json encoded
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// 1=revoked,0=not revoked
    /// </summary>
    public bool Revoked { get; set; }

    /// <summary>
    /// context values that change/govern how access token are used
    /// </summary>
    public string? Context { get; set; }
}
