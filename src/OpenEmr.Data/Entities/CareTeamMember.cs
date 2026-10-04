using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Stores members of a care team for a patient
/// </summary>
public partial class CareTeamMember
{
    public int Id { get; set; }

    public int CareTeamId { get; set; }

    /// <summary>
    /// fk to users.id represents a provider or staff member
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// fk to contact.id which represents a contact person not in users or facility table
    /// </summary>
    public long? ContactId { get; set; }

    /// <summary>
    /// fk to list_options.option_id WHERE list_id=care_team_roles
    /// </summary>
    public string Role { get; set; } = null!;

    /// <summary>
    /// fk to facility.id represents an organization or location
    /// </summary>
    public long? FacilityId { get; set; }

    public DateOnly? ProviderSince { get; set; }

    /// <summary>
    /// fk to list_options.option_id where list_id=Care_Team_Status
    /// </summary>
    public string? Status { get; set; }

    public DateTime? DateCreated { get; set; }

    public DateTime? DateUpdated { get; set; }

    /// <summary>
    /// fk to users.id and is the user that added this team member
    /// </summary>
    public long? CreatedBy { get; set; }

    /// <summary>
    /// fk to users.id and is the user that last updated this team member
    /// </summary>
    public long? UpdatedBy { get; set; }

    public string? Note { get; set; }
}
