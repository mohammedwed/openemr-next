using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Core person demographics - contact info in contact_telecom
/// </summary>
public partial class Person
{
    public long Id { get; set; }

    public byte[]? Uuid { get; set; }

    /// <summary>
    /// Mr., Mrs., Dr., etc.
    /// </summary>
    public string? Title { get; set; }

    public string? FirstName { get; set; }

    public string? MiddleName { get; set; }

    public string? LastName { get; set; }

    /// <summary>
    /// Name person prefers to be called
    /// </summary>
    public string? PreferredName { get; set; }

    public string? Gender { get; set; }

    public DateOnly? BirthDate { get; set; }

    public DateOnly? DeathDate { get; set; }

    public string? MaritalStatus { get; set; }

    public string? Race { get; set; }

    public string? Ethnicity { get; set; }

    /// <summary>
    /// ISO 639-1 code
    /// </summary>
    public string? PreferredLanguage { get; set; }

    /// <summary>
    /// Communication preferences/needs
    /// </summary>
    public string? Communication { get; set; }

    /// <summary>
    /// Should be encrypted in application
    /// </summary>
    public string? Ssn { get; set; }

    /// <summary>
    /// 1=active, 0=inactive
    /// </summary>
    public bool? Active { get; set; }

    public string? InactiveReason { get; set; }

    public DateTime? InactiveDate { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// users.id
    /// </summary>
    public long? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    /// <summary>
    /// users.id
    /// </summary>
    public long? UpdatedBy { get; set; }
}
