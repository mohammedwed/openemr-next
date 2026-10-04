using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class DocumentsLegalCategory
{
    public uint DlcId { get; set; }

    /// <summary>
    /// 1 category 2 subcategory
    /// </summary>
    public uint DlcCategoryType { get; set; }

    public string DlcCategoryName { get; set; } = null!;

    public uint? DlcCategoryParent { get; set; }
}
