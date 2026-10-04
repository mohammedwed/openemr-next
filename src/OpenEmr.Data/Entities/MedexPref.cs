using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class MedexPref
{
    public int? MedExId { get; set; }

    public string? MeUsername { get; set; }

    public string? MeApiKey { get; set; }

    public string? MeFacilities { get; set; }

    public string? MeProviders { get; set; }

    public string? MeHipaaDefaultOverride { get; set; }

    public int PhoneCountryCode { get; set; }

    public string? MsgsDefaultYes { get; set; }

    public string? PostcardsLocal { get; set; }

    public string? PostcardsRemote { get; set; }

    public string? LabelsLocal { get; set; }

    public string? LabelsChoice { get; set; }

    public sbyte? CombineTime { get; set; }

    public string? PostcardTop { get; set; }

    public string? Status { get; set; }

    public DateTime MedExLastupdated { get; set; }
}
