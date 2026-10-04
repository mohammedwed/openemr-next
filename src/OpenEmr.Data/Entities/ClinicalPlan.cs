using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ClinicalPlan
{
    /// <summary>
    /// Unique and maps to list_options list clinical_plans
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// 0 is default for all patients, while &gt; 0 is id from patient_data table
    /// </summary>
    public long Pid { get; set; }

    /// <summary>
    /// Normal Activation Flag
    /// </summary>
    public bool? NormalFlag { get; set; }

    /// <summary>
    /// Clinical Quality Measure flag (unable to customize per patient)
    /// </summary>
    public bool? CqmFlag { get; set; }

    /// <summary>
    /// 2011 Clinical Quality Measure flag (unable to customize per patient)
    /// </summary>
    public bool? Cqm2011Flag { get; set; }

    /// <summary>
    /// 2014 Clinical Quality Measure flag (unable to customize per patient)
    /// </summary>
    public bool? Cqm2014Flag { get; set; }

    /// <summary>
    /// Clinical Quality Measure Group Identifier
    /// </summary>
    public string CqmMeasureGroup { get; set; } = null!;
}
