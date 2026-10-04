using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class EligibilityVerification
{
    public long VerificationId { get; set; }

    public string? ResponseId { get; set; }

    public long? InsuranceId { get; set; }

    public DateTime? EligibilityCheckDate { get; set; }

    public int? Copay { get; set; }

    public int? Deductible { get; set; }

    public string? Deductiblemet { get; set; }

    public DateOnly? CreateDate { get; set; }
}
