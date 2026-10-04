using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// fhir export jobs
/// </summary>
public partial class ExportJob
{
    public long Id { get; set; }

    public byte[]? Uuid { get; set; }

    public string UserId { get; set; } = null!;

    public string ClientId { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime? StartTime { get; set; }

    public DateTime? ResourceIncludeTime { get; set; }

    public string OutputFormat { get; set; } = null!;

    public string RequestUri { get; set; } = null!;

    public string? Resources { get; set; }

    public string? Output { get; set; }

    public string? Errors { get; set; }

    public string? AccessTokenId { get; set; }
}
