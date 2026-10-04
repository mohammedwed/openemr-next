using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class User
{
    public long Id { get; set; }

    public byte[]? Uuid { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public sbyte? Authorized { get; set; }

    public string? Info { get; set; }

    public sbyte? Source { get; set; }

    public string? Fname { get; set; }

    public string? Mname { get; set; }

    public string? Lname { get; set; }

    public string? Suffix { get; set; }

    public string? Federaltaxid { get; set; }

    public string? Federaldrugid { get; set; }

    public string? Upin { get; set; }

    public string? Facility { get; set; }

    public int FacilityId { get; set; }

    public int SeeAuth { get; set; }

    public bool? Active { get; set; }

    public string? Npi { get; set; }

    public string? Title { get; set; }

    public string? Specialty { get; set; }

    public string? Billname { get; set; }

    public string? Email { get; set; }

    public string EmailDirect { get; set; } = null!;

    public string? GoogleSigninEmail { get; set; }

    public string? Url { get; set; }

    public string? Assistant { get; set; }

    public string? Organization { get; set; }

    public string? Valedictory { get; set; }

    public string? Street { get; set; }

    public string? Streetb { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Zip { get; set; }

    /// <summary>
    /// ISO 3166-1 alpha-2 country code for address but can take entire country name for now
    /// </summary>
    public string? CountryCode { get; set; }

    public string? Street2 { get; set; }

    public string? Streetb2 { get; set; }

    public string? City2 { get; set; }

    public string? State2 { get; set; }

    public string? Zip2 { get; set; }

    /// <summary>
    /// ISO 3166-1 alpha-2 country code for address but can take entire country name for now
    /// </summary>
    public string? CountryCode2 { get; set; }

    public string? Phone { get; set; }

    public string? Fax { get; set; }

    public string? Phonew1 { get; set; }

    public string? Phonew2 { get; set; }

    public string? Phonecell { get; set; }

    public string? Notes { get; set; }

    public sbyte CalUi { get; set; }

    public string Taxonomy { get; set; } = null!;

    /// <summary>
    /// 1 = appears in calendar
    /// </summary>
    public bool Calendar { get; set; }

    public string AbookType { get; set; } = null!;

    public string DefaultWarehouse { get; set; } = null!;

    public string Irnpool { get; set; } = null!;

    public string? StateLicenseNumber { get; set; }

    public string? WenoProvId { get; set; }

    public string? NewcropUserRole { get; set; }

    public bool? Cpoe { get; set; }

    public string? PhysicianType { get; set; }

    public string MainMenuRole { get; set; } = null!;

    public string PatientMenuRole { get; set; } = null!;

    public bool PortalUser { get; set; }

    public int SupervisorId { get; set; }

    public string? BillingFacility { get; set; }

    public int BillingFacilityId { get; set; }

    public DateTime DateCreated { get; set; }

    public DateTime LastUpdated { get; set; }
}
