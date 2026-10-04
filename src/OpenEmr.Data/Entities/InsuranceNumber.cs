using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class InsuranceNumber
{
    public int Id { get; set; }

    public int ProviderId { get; set; }

    public int? InsuranceCompanyId { get; set; }

    public string? ProviderNumber { get; set; }

    public string? RenderingProviderNumber { get; set; }

    public string? GroupNumber { get; set; }

    public string? ProviderNumberType { get; set; }

    public string? RenderingProviderNumberType { get; set; }
}
