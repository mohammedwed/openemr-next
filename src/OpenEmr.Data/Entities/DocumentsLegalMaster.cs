using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// List of Master Docs to be signed
/// </summary>
public partial class DocumentsLegalMaster
{
    public uint? DlmCategory { get; set; }

    public uint? DlmSubcategory { get; set; }

    public uint DlmDocumentId { get; set; }

    public string DlmDocumentName { get; set; } = null!;

    public string DlmFilepath { get; set; } = null!;

    public uint? DlmFacility { get; set; }

    public uint? DlmProvider { get; set; }

    public double DlmSignHeight { get; set; }

    public double DlmSignWidth { get; set; }

    public string DlmFilename { get; set; } = null!;

    public DateTime DlmEffectiveDate { get; set; }

    public uint DlmVersion { get; set; }

    public string Content { get; set; } = null!;

    /// <summary>
    /// 0-Yes 1-No
    /// </summary>
    public string? DlmSavedsign { get; set; }

    /// <summary>
    /// 0-Yes 1-No
    /// </summary>
    public string? DlmReview { get; set; }

    /// <summary>
    /// 0-Provider Uploaded,1-Patient Uploaded
    /// </summary>
    public sbyte? DlmUploadType { get; set; }
}
