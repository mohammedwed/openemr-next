using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class LogCommentEncrypt
{
    public int Id { get; set; }

    public int LogId { get; set; }

    public string Encrypt { get; set; } = null!;

    public string? Checksum { get; set; }

    public string? ChecksumApi { get; set; }

    /// <summary>
    /// 0 for mycrypt and 1 for openssl
    /// </summary>
    public sbyte Version { get; set; }
}
