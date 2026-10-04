using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class CodeType
{
    /// <summary>
    /// short alphanumeric name
    /// </summary>
    public string CtKey { get; set; } = null!;

    /// <summary>
    /// numeric identifier
    /// </summary>
    public int CtId { get; set; }

    /// <summary>
    /// sort order
    /// </summary>
    public int CtSeq { get; set; }

    /// <summary>
    /// length of modifier field
    /// </summary>
    public int CtMod { get; set; }

    /// <summary>
    /// ct_key of justify type, if any
    /// </summary>
    public string CtJust { get; set; } = null!;

    /// <summary>
    /// formatting mask for code values
    /// </summary>
    public string CtMask { get; set; } = null!;

    /// <summary>
    /// 1 if fees are used
    /// </summary>
    public bool CtFee { get; set; }

    /// <summary>
    /// 1 if can relate to other code types
    /// </summary>
    public bool CtRel { get; set; }

    /// <summary>
    /// 1 if to be hidden in the fee sheet
    /// </summary>
    public bool CtNofs { get; set; }

    /// <summary>
    /// 1 if this is a diagnosis type
    /// </summary>
    public bool CtDiag { get; set; }

    /// <summary>
    /// 1 if this is active
    /// </summary>
    public bool? CtActive { get; set; }

    /// <summary>
    /// label of this code type
    /// </summary>
    public string CtLabel { get; set; } = null!;

    /// <summary>
    /// 0 if stored codes in codes tables, 1 or greater if codes stored in external tables
    /// </summary>
    public bool CtExternal { get; set; }

    /// <summary>
    /// 1 if this is used in claims
    /// </summary>
    public bool CtClaim { get; set; }

    /// <summary>
    /// 1 if this is a procedure type
    /// </summary>
    public bool CtProc { get; set; }

    /// <summary>
    /// 1 if this is a clinical term
    /// </summary>
    public bool CtTerm { get; set; }

    /// <summary>
    /// 1 if this code type is used as a medical problem
    /// </summary>
    public bool CtProblem { get; set; }

    /// <summary>
    /// 1 if this code type is used as a medication
    /// </summary>
    public bool CtDrug { get; set; }
}
