using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class OnsiteDocument
{
    public uint Id { get; set; }

    public ulong? Pid { get; set; }

    public uint? Facility { get; set; }

    public uint? Provider { get; set; }

    public uint? Encounter { get; set; }

    public DateTime CreateDate { get; set; }

    public string DocType { get; set; } = null!;

    public ushort PatientSignedStatus { get; set; }

    public DateTime? PatientSignedTime { get; set; }

    public DateTime? AuthorizeSignedTime { get; set; }

    public short AcceptSignedStatus { get; set; }

    public string AuthorizingSignator { get; set; } = null!;

    public DateTime? ReviewDate { get; set; }

    public string DenialReason { get; set; } = null!;

    public string? AuthorizedSignature { get; set; }

    public string? PatientSignature { get; set; }

    public byte[]? FullDocument { get; set; }

    public string FileName { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public string? TemplateData { get; set; }
}
