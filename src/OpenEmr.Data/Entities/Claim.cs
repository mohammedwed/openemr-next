using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Claim
{
    public long PatientId { get; set; }

    public int EncounterId { get; set; }

    /// <summary>
    /// Claim version, incremented in code
    /// </summary>
    public uint Version { get; set; }

    public int PayerId { get; set; }

    public sbyte Status { get; set; }

    public sbyte PayerType { get; set; }

    public sbyte BillProcess { get; set; }

    public DateTime? BillTime { get; set; }

    public DateTime? ProcessTime { get; set; }

    public string? ProcessFile { get; set; }

    public string? Target { get; set; }

    public int X12PartnerId { get; set; }

    /// <summary>
    /// This claims form claim data
    /// </summary>
    public string? SubmittedClaim { get; set; }
}
