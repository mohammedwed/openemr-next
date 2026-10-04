using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class BackgroundService
{
    public string Name { get; set; } = null!;

    /// <summary>
    /// name for reports
    /// </summary>
    public string Title { get; set; } = null!;

    public bool Active { get; set; }

    /// <summary>
    /// True indicates managed service is busy. Skip this interval
    /// </summary>
    public bool? Running { get; set; }

    public DateTime NextRun { get; set; }

    /// <summary>
    /// minimum number of minutes between function calls,0=manual mode
    /// </summary>
    public int ExecuteInterval { get; set; }

    /// <summary>
    /// name of background service function
    /// </summary>
    public string Function { get; set; } = null!;

    /// <summary>
    /// include file (if necessary)
    /// </summary>
    public string? RequireOnce { get; set; }

    /// <summary>
    /// lower numbers will be run first
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Lease expiration. Compared with NOW() on acquire, so the stored value uses whatever session timezone is in effect (OpenEMR syncs it to gbl_time_zone). Set on acquire, cleared on release. Expired leases are automatically stolen by the next worker.
    /// </summary>
    public DateTime? LockExpiresAt { get; set; }
}
