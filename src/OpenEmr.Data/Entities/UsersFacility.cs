using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// joins users or patient_data to facility table
/// </summary>
public partial class UsersFacility
{
    public string Tablename { get; set; } = null!;

    public int TableId { get; set; }

    public int FacilityId { get; set; }

    public string WarehouseId { get; set; } = null!;
}
