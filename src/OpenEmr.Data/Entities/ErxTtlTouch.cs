using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Store records last update per patient data process
/// </summary>
public partial class ErxTtlTouch
{
    /// <summary>
    /// Patient record Id
    /// </summary>
    public ulong PatientId { get; set; }

    /// <summary>
    /// NewCrop eRx SOAP process
    /// </summary>
    public string Process { get; set; } = null!;

    /// <summary>
    /// Date and time of last process update for patient
    /// </summary>
    public DateTime Updated { get; set; }
}
