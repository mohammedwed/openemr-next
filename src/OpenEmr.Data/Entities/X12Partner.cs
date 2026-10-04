using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class X12Partner
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? IdNumber { get; set; }

    public string? X12SenderId { get; set; }

    public string? X12ReceiverId { get; set; }

    public string? ProcessingFormat { get; set; }

    /// <summary>
    /// User logon Required Indicator
    /// </summary>
    public string X12Isa01 { get; set; } = null!;

    /// <summary>
    /// User Logon
    /// </summary>
    public string X12Isa02 { get; set; } = null!;

    /// <summary>
    /// User password required Indicator
    /// </summary>
    public string X12Isa03 { get; set; } = null!;

    /// <summary>
    /// User Password
    /// </summary>
    public string X12Isa04 { get; set; } = null!;

    public string X12Isa05 { get; set; } = null!;

    public string X12Isa07 { get; set; } = null!;

    public string X12Isa14 { get; set; } = null!;

    public string X12Isa15 { get; set; } = null!;

    public string X12Gs02 { get; set; } = null!;

    public string X12Per06 { get; set; } = null!;

    public string X12Dtp03 { get; set; } = null!;

    public string? X12Gs03 { get; set; }

    public short? X12SubmitterId { get; set; }

    public string? X12SubmitterName { get; set; }

    public string? X12SftpLogin { get; set; }

    public string? X12SftpPass { get; set; }

    public string? X12SftpHost { get; set; }

    public string? X12SftpPort { get; set; }

    public string? X12SftpLocalDir { get; set; }

    public string? X12SftpRemoteDir { get; set; }

    public string? X12TokenEndpoint { get; set; }

    public string? X12EligibilityEndpoint { get; set; }

    public string? X12ClaimStatusEndpoint { get; set; }

    public string? X12AttachmentEndpoint { get; set; }

    public string? X12ClientId { get; set; }

    public string? X12ClientSecret { get; set; }
}
