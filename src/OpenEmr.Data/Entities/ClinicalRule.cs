using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ClinicalRule
{
    /// <summary>
    /// Unique and maps to list_options list clinical_rules
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// 0 is default for all patients, while &gt; 0 is id from patient_data table
    /// </summary>
    public long Pid { get; set; }

    /// <summary>
    /// Active Alert Widget Module flag - note not yet utilized
    /// </summary>
    public bool? ActiveAlertFlag { get; set; }

    /// <summary>
    /// Passive Alert Widget Module flag
    /// </summary>
    public bool? PassiveAlertFlag { get; set; }

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
    /// Clinical Quality Measure NQF identifier
    /// </summary>
    public string CqmNqfCode { get; set; } = null!;

    /// <summary>
    /// Clinical Quality Measure PQRI identifier
    /// </summary>
    public string CqmPqriCode { get; set; } = null!;

    /// <summary>
    /// Automated Measure Calculation flag (unable to customize per patient)
    /// </summary>
    public bool? AmcFlag { get; set; }

    /// <summary>
    /// 2011 Automated Measure Calculation flag for (unable to customize per patient)
    /// </summary>
    public bool? Amc2011Flag { get; set; }

    /// <summary>
    /// 2014 Automated Measure Calculation flag for (unable to customize per patient)
    /// </summary>
    public bool? Amc2014Flag { get; set; }

    /// <summary>
    /// 2015 Automated Measure Calculation flag for (unable to customize per patient)
    /// </summary>
    public bool? Amc2015Flag { get; set; }

    /// <summary>
    /// Automated Measure Calculation identifier (MU rule)
    /// </summary>
    public string AmcCode { get; set; } = null!;

    /// <summary>
    /// Automated Measure Calculation 2014 identifier (MU rule)
    /// </summary>
    public string AmcCode2014 { get; set; } = null!;

    /// <summary>
    /// Automated Measure Calculation 2014 identifier (MU rule)
    /// </summary>
    public string AmcCode2015 { get; set; } = null!;

    /// <summary>
    /// 2014 Stage 1 - Automated Measure Calculation flag for (unable to customize per patient)
    /// </summary>
    public bool? Amc2014Stage1Flag { get; set; }

    /// <summary>
    /// 2014 Stage 2 - Automated Measure Calculation flag for (unable to customize per patient)
    /// </summary>
    public bool? Amc2014Stage2Flag { get; set; }

    /// <summary>
    /// Clinical Reminder Module flag
    /// </summary>
    public bool? PatientReminderFlag { get; set; }

    public string BibliographicCitation { get; set; } = null!;

    /// <summary>
    /// Clinical Rule Developer
    /// </summary>
    public string Developer { get; set; } = null!;

    /// <summary>
    /// Clinical Rule Funding Source
    /// </summary>
    public string FundingSource { get; set; } = null!;

    /// <summary>
    /// Clinical Rule Release Version
    /// </summary>
    public string ReleaseVersion { get; set; } = null!;

    /// <summary>
    /// Clinical Rule Web Reference
    /// </summary>
    public string WebReference { get; set; } = null!;

    public string LinkedReferentialCds { get; set; } = null!;

    /// <summary>
    /// ACO link for access control
    /// </summary>
    public string AccessControl { get; set; } = null!;

    /// <summary>
    /// Description of how patient DOB is used by this rule
    /// </summary>
    public string? PatientDobUsage { get; set; }

    /// <summary>
    /// Description of how patient ethnicity is used by this rule
    /// </summary>
    public string? PatientEthnicityUsage { get; set; }

    /// <summary>
    /// Description of how patient health status assessments are used by this rule
    /// </summary>
    public string? PatientHealthStatusUsage { get; set; }

    /// <summary>
    /// Description of how patient gender identity information is used by this rule
    /// </summary>
    public string? PatientGenderIdentityUsage { get; set; }

    /// <summary>
    /// Description of how patient language information is used by this rule
    /// </summary>
    public string? PatientLanguageUsage { get; set; }

    /// <summary>
    /// Description of how patient race information is used by this rule
    /// </summary>
    public string? PatientRaceUsage { get; set; }

    /// <summary>
    /// Description of how patient birth sex information is used by this rule
    /// </summary>
    public string? PatientSexUsage { get; set; }

    /// <summary>
    /// Description of how patient sexual orientation is used by this rule
    /// </summary>
    public string? PatientSexualOrientationUsage { get; set; }

    /// <summary>
    /// Description of how patient social determinants of health are used by this rule
    /// </summary>
    public string? PatientSodhUsage { get; set; }
}
