using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Links ServiceRequests to supporting clinical information
/// </summary>
public partial class ProcedureOrderRelationship
{
    public int Id { get; set; }

    /// <summary>
    /// Links to procedure_order.procedure_order_id
    /// </summary>
    public long ProcedureOrderId { get; set; }

    /// <summary>
    /// FHIR resource type (Observation, Condition, etc.)
    /// </summary>
    public string ResourceType { get; set; } = null!;

    /// <summary>
    /// UUID of the related resource
    /// </summary>
    public byte[] ResourceUuid { get; set; } = null!;

    /// <summary>
    /// Type of relationship
    /// </summary>
    public string? Relationship { get; set; }

    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// User who created this link
    /// </summary>
    public long? CreatedBy { get; set; }
}
