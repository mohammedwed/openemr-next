using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Void
{
    public long VoidId { get; set; }

    /// <summary>
    /// references patient_data.pid
    /// </summary>
    public long PatientId { get; set; }

    /// <summary>
    /// references form_encounter.encounter
    /// </summary>
    public long EncounterId { get; set; }

    /// <summary>
    /// checkout,receipt and maybe other options later
    /// </summary>
    public string WhatVoided { get; set; } = null!;

    /// <summary>
    /// time of original action that is now voided
    /// </summary>
    public DateTime? DateOriginal { get; set; }

    /// <summary>
    /// time of void action
    /// </summary>
    public DateTime DateVoided { get; set; }

    /// <summary>
    /// references users.id
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// for checkout,receipt total voided adjustments
    /// </summary>
    public decimal Amount1 { get; set; }

    /// <summary>
    /// for checkout,receipt total voided payments
    /// </summary>
    public decimal Amount2 { get; set; }

    /// <summary>
    /// for checkout,receipt the old invoice refno
    /// </summary>
    public string? OtherInfo { get; set; }

    public string? Reason { get; set; }

    public string? Notes { get; set; }
}
