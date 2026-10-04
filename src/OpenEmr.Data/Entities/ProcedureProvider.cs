using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ProcedureProvider
{
    public long Ppid { get; set; }

    public byte[]? Uuid { get; set; }

    public string Name { get; set; } = null!;

    public string Npi { get; set; } = null!;

    /// <summary>
    /// Sending application ID (MSH-3.1)
    /// </summary>
    public string SendAppId { get; set; } = null!;

    /// <summary>
    /// Sending facility ID (MSH-4.1)
    /// </summary>
    public string SendFacId { get; set; } = null!;

    /// <summary>
    /// Receiving application ID (MSH-5.1)
    /// </summary>
    public string RecvAppId { get; set; } = null!;

    /// <summary>
    /// Receiving facility ID (MSH-6.1)
    /// </summary>
    public string RecvFacId { get; set; } = null!;

    /// <summary>
    /// Debugging or Production (MSH-11)
    /// </summary>
    public string DorP { get; set; } = null!;

    /// <summary>
    /// Bidirectional or Results-only
    /// </summary>
    public string Direction { get; set; } = null!;

    public string Protocol { get; set; } = null!;

    public string RemoteHost { get; set; } = null!;

    public string Login { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string OrdersPath { get; set; } = null!;

    public string ResultsPath { get; set; } = null!;

    public string? Notes { get; set; }

    public long LabDirector { get; set; }

    public bool? Active { get; set; }

    public string? Type { get; set; }

    public DateTime DateCreated { get; set; }

    public DateTime LastUpdated { get; set; }
}
