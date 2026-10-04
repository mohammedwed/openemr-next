using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ProAssessment
{
    public int Id { get; set; }

    /// <summary>
    /// unique id for specific instrument, pulled from assessment center API
    /// </summary>
    public string FormOid { get; set; } = null!;

    /// <summary>
    /// pulled from assessment center API
    /// </summary>
    public string FormName { get; set; } = null!;

    /// <summary>
    /// ID for user that orders the form
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// deadline to complete the form, will be used when sending notification and reminders
    /// </summary>
    public DateTime Deadline { get; set; }

    /// <summary>
    /// ID for patient to order the form for
    /// </summary>
    public int PatientId { get; set; }

    /// <summary>
    /// unique id for this specific assessment, pulled from assessment center API
    /// </summary>
    public string AssessmentOid { get; set; } = null!;

    /// <summary>
    /// ordered or completed
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// T-Score for the assessment
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Standard error for the score
    /// </summary>
    public double Error { get; set; }

    /// <summary>
    /// timestamp recording the creation time of this assessment
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// this field indicates the completion time when the status is completed
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
