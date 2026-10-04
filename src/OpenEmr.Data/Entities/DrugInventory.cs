using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class DrugInventory
{
    public int InventoryId { get; set; }

    public int DrugId { get; set; }

    public string? LotNumber { get; set; }

    public DateOnly? Expiration { get; set; }

    public string? Manufacturer { get; set; }

    public int OnHand { get; set; }

    public string WarehouseId { get; set; } = null!;

    public long VendorId { get; set; }

    public DateOnly? LastNotify { get; set; }

    public DateOnly? DestroyDate { get; set; }

    public string? DestroyMethod { get; set; }

    public string? DestroyWitness { get; set; }

    public string? DestroyNotes { get; set; }
}
