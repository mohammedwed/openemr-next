using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class EmployerDatum
{
    public long Id { get; set; }

    /// <summary>
    /// UUID for this employer record, for data exchange purposes
    /// </summary>
    public byte[]? Uuid { get; set; }

    public string? Name { get; set; }

    public string? Street { get; set; }

    public string? StreetLine2 { get; set; }

    public string? PostalCode { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Country { get; set; }

    public DateTime? Date { get; set; }

    public long Pid { get; set; }

    /// <summary>
    /// Employment start date for patient
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Employment end date for patient
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Employment Occupation fk to list_options.option_id where list_id=OccupationODH
    /// </summary>
    public string? Occupation { get; set; }

    /// <summary>
    /// Employment Industry fk to list_options.option_id where list_id=IndustryODH
    /// </summary>
    public string? Industry { get; set; }

    /// <summary>
    /// fk to users.id for the user that entered in the employer data
    /// </summary>
    public int? CreatedBy { get; set; }
}
