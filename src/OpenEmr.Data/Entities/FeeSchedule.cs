using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FeeSchedule
{
    public long Id { get; set; }

    public int InsuranceCompanyId { get; set; }

    public string? Plan { get; set; }

    public string? Code { get; set; }

    public string? Modifier { get; set; }

    public string? Type { get; set; }

    public decimal? Fee { get; set; }

    public DateOnly? EffectiveDate { get; set; }
}
