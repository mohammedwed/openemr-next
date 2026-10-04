using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormMiscBillingOption
{
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    public long? Pid { get; set; }

    public string? User { get; set; }

    public string? Groupname { get; set; }

    public sbyte? Authorized { get; set; }

    public sbyte? Activity { get; set; }

    public bool? EmploymentRelated { get; set; }

    public bool? AutoAccident { get; set; }

    public string? AccidentState { get; set; }

    public bool? OtherAccident { get; set; }

    public string? MedicaidReferralCode { get; set; }

    public bool? EpsdtFlag { get; set; }

    public string? ProviderQualifierCode { get; set; }

    public int? ProviderId { get; set; }

    public bool? OutsideLab { get; set; }

    public decimal? LabAmount { get; set; }

    public bool? IsUnableToWork { get; set; }

    public DateOnly? OnsetDate { get; set; }

    public DateOnly? DateInitialTreatment { get; set; }

    public DateOnly? OffWorkFrom { get; set; }

    public DateOnly? OffWorkTo { get; set; }

    public bool? IsHospitalized { get; set; }

    public DateOnly? HospitalizationDateFrom { get; set; }

    public DateOnly? HospitalizationDateTo { get; set; }

    public string? ResubmissionCode { get; set; }

    public string? OriginalReferenceNumber { get; set; }

    public string? PriorAuthNumber { get; set; }

    public string? Comments { get; set; }

    public bool? ReplacementClaim { get; set; }

    public string? IcnResubmissionNumber { get; set; }

    public string? Box14DateQual { get; set; }

    public string? Box15DateQual { get; set; }

    public long? Encounter { get; set; }
}
