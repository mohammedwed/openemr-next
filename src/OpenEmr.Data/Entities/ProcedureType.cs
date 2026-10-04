using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ProcedureType
{
    public long ProcedureTypeId { get; set; }

    /// <summary>
    /// references procedure_type.procedure_type_id
    /// </summary>
    public long Parent { get; set; }

    /// <summary>
    /// name for this category, procedure or result type
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// references procedure_providers.ppid, 0 means default to parent
    /// </summary>
    public long LabId { get; set; }

    /// <summary>
    /// code identifying this procedure
    /// </summary>
    public string ProcedureCode { get; set; } = null!;

    /// <summary>
    /// see list proc_type
    /// </summary>
    public string ProcedureType1 { get; set; } = null!;

    /// <summary>
    /// where to do injection, e.g. arm, buttock
    /// </summary>
    public string BodySite { get; set; } = null!;

    /// <summary>
    /// blood, urine, saliva, etc.
    /// </summary>
    public string Specimen { get; set; } = null!;

    /// <summary>
    /// oral, injection
    /// </summary>
    public string RouteAdmin { get; set; } = null!;

    /// <summary>
    /// left, right, ...
    /// </summary>
    public string Laterality { get; set; } = null!;

    /// <summary>
    /// descriptive text for procedure_code
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// industry standard code type and code (e.g. CPT4:12345)
    /// </summary>
    public string StandardCode { get; set; } = null!;

    /// <summary>
    /// suggested code(s) for followup services if result is abnormal
    /// </summary>
    public string RelatedCode { get; set; } = null!;

    /// <summary>
    /// default for procedure_result.units
    /// </summary>
    public string Units { get; set; } = null!;

    /// <summary>
    /// default for procedure_result.range
    /// </summary>
    public string Range { get; set; } = null!;

    /// <summary>
    /// sequence number for ordering
    /// </summary>
    public int Seq { get; set; }

    /// <summary>
    /// 1=active, 0=inactive
    /// </summary>
    public bool? Activity { get; set; }

    /// <summary>
    /// additional notes to enhance description
    /// </summary>
    public string Notes { get; set; } = null!;

    public string? Transport { get; set; }

    public string? ProcedureTypeName { get; set; }
}
