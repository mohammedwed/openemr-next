using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Ccdum
{
    public int Id { get; set; }

    public byte[]? Uuid { get; set; }

    public long? Pid { get; set; }

    public long? Encounter { get; set; }

    public string? CcdaData { get; set; }

    public string? Time { get; set; }

    public short? Status { get; set; }

    public DateTime UpdatedDate { get; set; }

    public string? UserId { get; set; }

    public string? CouchDocid { get; set; }

    public string? CouchRevid { get; set; }

    public string? Hash { get; set; }

    public sbyte View { get; set; }

    public sbyte Transfer { get; set; }

    public sbyte EmrTransfer { get; set; }

    /// <summary>
    /// 0-&gt;No,1-&gt;Yes
    /// </summary>
    public sbyte Encrypted { get; set; }

    /// <summary>
    /// fk to transaction referral record
    /// </summary>
    public long? TransactionId { get; set; }
}
