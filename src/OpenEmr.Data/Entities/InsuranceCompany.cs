using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class InsuranceCompany
{
    public int Id { get; set; }

    public byte[]? Uuid { get; set; }

    public string? Name { get; set; }

    public string? Attn { get; set; }

    public string? CmsId { get; set; }

    public int? InsTypeCode { get; set; }

    public string? X12ReceiverId { get; set; }

    public int? X12DefaultPartnerId { get; set; }

    public string? AltCmsId { get; set; }

    public bool Inactive { get; set; }

    public string? EligibilityId { get; set; }

    public int? X12DefaultEligibilityId { get; set; }

    /// <summary>
    /// HL7 Source of Payment for eCQMs
    /// </summary>
    public int? CqmSop { get; set; }

    public DateTime DateCreated { get; set; }

    public DateTime LastUpdated { get; set; }
}
