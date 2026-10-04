using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class OnetimeAuth
{
    public int Id { get; set; }

    public long? Pid { get; set; }

    public long? CreateUserId { get; set; }

    public string? Context { get; set; }

    public int AccessCount { get; set; }

    public string? RemoteIp { get; set; }

    /// <summary>
    /// Max 10 numeric. Default 6
    /// </summary>
    public string? OnetimePin { get; set; }

    public string? OnetimeToken { get; set; }

    public string? RedirectUrl { get; set; }

    public int? Expires { get; set; }

    public DateTime? DateCreated { get; set; }

    public DateTime? LastAccessed { get; set; }

    /// <summary>
    /// context scope for this token
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// profile of scope for this token
    /// </summary>
    public string? Profile { get; set; }

    /// <summary>
    /// JSON array of actions that can be performed with this token
    /// </summary>
    public string? OnetimeActions { get; set; }
}
