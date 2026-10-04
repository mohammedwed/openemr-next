using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Form
{
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    public long? Encounter { get; set; }

    public string? FormName { get; set; }

    public long? FormId { get; set; }

    public long? Pid { get; set; }

    public string? User { get; set; }

    public string? Groupname { get; set; }

    public sbyte? Authorized { get; set; }

    /// <summary>
    /// flag indicates form has been deleted
    /// </summary>
    public sbyte Deleted { get; set; }

    public string? Formdir { get; set; }

    public int? TherapyGroupId { get; set; }

    /// <summary>
    /// references lists.id to identify a case
    /// </summary>
    public long IssueId { get; set; }

    /// <summary>
    /// references users.id to identify a provider
    /// </summary>
    public long ProviderId { get; set; }
}
