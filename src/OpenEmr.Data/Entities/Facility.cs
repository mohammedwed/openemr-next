using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Facility
{
    public int Id { get; set; }

    public byte[]? Uuid { get; set; }

    public string? Name { get; set; }

    public string? Phone { get; set; }

    public string? Fax { get; set; }

    public string? Street { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? PostalCode { get; set; }

    public string CountryCode { get; set; } = null!;

    public string? FederalEin { get; set; }

    public string? Website { get; set; }

    public string? Email { get; set; }

    public bool? ServiceLocation { get; set; }

    public bool? BillingLocation { get; set; }

    public bool? AcceptsAssignment { get; set; }

    public sbyte? PosCode { get; set; }

    public string? X12SenderId { get; set; }

    public string? Attn { get; set; }

    public string? DomainIdentifier { get; set; }

    public string? FacilityNpi { get; set; }

    public string? FacilityTaxonomy { get; set; }

    public string TaxIdType { get; set; } = null!;

    public string Color { get; set; } = null!;

    /// <summary>
    /// 0-Not Set as business entity 1-Set as business entity
    /// </summary>
    public int PrimaryBusinessEntity { get; set; }

    public string? FacilityCode { get; set; }

    public bool? ExtraValidation { get; set; }

    public string? MailStreet { get; set; }

    public string? MailStreet2 { get; set; }

    public string? MailCity { get; set; }

    public string? MailState { get; set; }

    public string? MailZip { get; set; }

    /// <summary>
    /// HIEs CCDA and FHIR an OID is required/wanted
    /// </summary>
    public string Oid { get; set; } = null!;

    public string? Iban { get; set; }

    public string? Info { get; set; }

    public string? WenoId { get; set; }

    public bool Inactive { get; set; }

    public DateTime DateCreated { get; set; }

    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Organization type as defined by HL7 Value Set: OrganizationType
    /// </summary>
    public string OrganizationType { get; set; } = null!;
}
