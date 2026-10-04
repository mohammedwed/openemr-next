using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class BenefitEligibility
{
    public long ResponseId { get; set; }

    public long VerificationId { get; set; }

    public string? Type { get; set; }

    public string? BenefitType { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? CoverageLevel { get; set; }

    public string? CoverageType { get; set; }

    public string? PlanType { get; set; }

    public string? PlanDescription { get; set; }

    public string? CoveragePeriod { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Percent { get; set; }

    public string? NetworkInd { get; set; }

    public string? Message { get; set; }

    public string? ResponseStatus { get; set; }

    public DateOnly? ResponseCreateDate { get; set; }

    public DateOnly? ResponseModifyDate { get; set; }
}
