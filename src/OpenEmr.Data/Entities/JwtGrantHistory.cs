using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Holds JWT authorization grant ids to prevent replay attacks
/// </summary>
public partial class JwtGrantHistory
{
    public int Id { get; set; }

    /// <summary>
    /// Unique JWT id
    /// </summary>
    public string Jti { get; set; } = null!;

    /// <summary>
    /// FK oauth2_clients.client_id
    /// </summary>
    public string ClientId { get; set; } = null!;

    /// <summary>
    /// jwt exp claim when the jwt expires
    /// </summary>
    public DateTime? JtiExp { get; set; }

    /// <summary>
    /// datetime the grant authorization was requested
    /// </summary>
    public DateTime CreationDate { get; set; }
}
