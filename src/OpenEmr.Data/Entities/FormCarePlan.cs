using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormCarePlan
{
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    public long? Pid { get; set; }

    public string? Encounter { get; set; }

    public string? User { get; set; }

    public string? Groupname { get; set; }

    public sbyte? Authorized { get; set; }

    public sbyte? Activity { get; set; }

    public string? Code { get; set; }

    public string? Codetext { get; set; }

    public string? Description { get; set; }

    public string? ExternalId { get; set; }

    public string? CarePlanType { get; set; }

    public string? NoteRelatedTo { get; set; }

    public DateTime? DateEnd { get; set; }

    public string? ReasonCode { get; set; }

    public string? ReasonDescription { get; set; }

    /// <summary>
    /// The date the reason was recorded
    /// </summary>
    public DateTime? ReasonDateLow { get; set; }

    /// <summary>
    /// The date the explanation reason for the care plan entry value ends
    /// </summary>
    public DateTime? ReasonDateHigh { get; set; }

    public string? ReasonStatus { get; set; }

    /// <summary>
    /// Care Plan status (e.g., draft, active, completed, etc)
    /// </summary>
    public string? PlanStatus { get; set; }

    /// <summary>
    /// Target or Achieve-by date for the goal
    /// </summary>
    public DateTime? ProposedDate { get; set; }

    /// <summary>
    /// Expected engagement category with the patient based upon the care plan type
    /// </summary>
    public string? PlanEngagementCategory { get; set; }
}
