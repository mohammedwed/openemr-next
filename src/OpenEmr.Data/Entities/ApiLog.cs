using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ApiLog
{
    public long Id { get; set; }

    public int LogId { get; set; }

    public long UserId { get; set; }

    /// <summary>
    /// oauth_clients.client_id of the API client that made the request
    /// </summary>
    public string ClientId { get; set; } = null!;

    public long PatientId { get; set; }

    public string IpAddress { get; set; } = null!;

    public string Method { get; set; } = null!;

    public string Request { get; set; } = null!;

    public string? RequestUrl { get; set; }

    public string? RequestBody { get; set; }

    public string? Response { get; set; }

    public DateTime? CreatedTime { get; set; }
}
