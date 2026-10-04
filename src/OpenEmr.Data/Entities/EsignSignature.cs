using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class EsignSignature
{
    public int Id { get; set; }

    /// <summary>
    /// Table row ID for signature
    /// </summary>
    public int Tid { get; set; }

    /// <summary>
    /// table name for the signature
    /// </summary>
    public string Table { get; set; } = null!;

    /// <summary>
    /// user id for the signing user
    /// </summary>
    public int Uid { get; set; }

    /// <summary>
    /// datetime of the signature action
    /// </summary>
    public DateTime Datetime { get; set; }

    /// <summary>
    /// sig, lock or amendment
    /// </summary>
    public bool IsLock { get; set; }

    /// <summary>
    /// amendment text, if any
    /// </summary>
    public string? Amendment { get; set; }

    /// <summary>
    /// hash of signed data
    /// </summary>
    public string Hash { get; set; } = null!;

    /// <summary>
    /// hash of signature itself
    /// </summary>
    public string SignatureHash { get; set; } = null!;
}
