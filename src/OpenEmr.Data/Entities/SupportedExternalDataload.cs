using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class SupportedExternalDataload
{
    public ulong LoadId { get; set; }

    public string LoadType { get; set; } = null!;

    public string LoadSource { get; set; } = null!;

    public DateOnly LoadReleaseDate { get; set; }

    public string LoadFilename { get; set; } = null!;

    public string LoadChecksum { get; set; } = null!;
}
