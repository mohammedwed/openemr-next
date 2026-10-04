using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class DocumentsLegalDetail
{
    public uint DldId { get; set; }

    public uint? DldPid { get; set; }

    public uint? DldFacility { get; set; }

    public uint? DldProvider { get; set; }

    public uint? DldEncounter { get; set; }

    public uint DldMasterDocid { get; set; }

    /// <summary>
    /// 0-Not Signed or Cannot Sign(Layout),1-Signed,2-Ready to sign,3-Denied(Pat Regi),4-Patient Upload,10-Save(Layout)
    /// </summary>
    public ushort DldSigned { get; set; }

    public DateTime DldSignedTime { get; set; }

    public string? DldFilepath { get; set; }

    public string DldFilename { get; set; } = null!;

    public string DldSigningPerson { get; set; } = null!;

    /// <summary>
    /// Sign flow level
    /// </summary>
    public int DldSignLevel { get; set; }

    /// <summary>
    /// Layout sign position
    /// </summary>
    public string DldContent { get; set; } = null!;

    /// <summary>
    /// The filled details in the fdf file is stored here.Patient Registration Screen
    /// </summary>
    public byte[] DldFileForPdfGeneration { get; set; } = null!;

    public string? DldDenialReason { get; set; }

    public sbyte DldMoved { get; set; }

    /// <summary>
    /// Patient comments stored here
    /// </summary>
    public string? DldPatientComments { get; set; }
}
