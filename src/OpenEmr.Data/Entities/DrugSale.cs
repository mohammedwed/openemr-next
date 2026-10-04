using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class DrugSale
{
    /// <summary>
    /// UUID for this drug sales record, for data exchange purposes
    /// </summary>
    public byte[]? Uuid { get; set; }

    public int SaleId { get; set; }

    public int DrugId { get; set; }

    public int InventoryId { get; set; }

    public int PrescriptionId { get; set; }

    public long Pid { get; set; }

    public int Encounter { get; set; }

    public string? User { get; set; }

    public DateOnly SaleDate { get; set; }

    public int Quantity { get; set; }

    public decimal Fee { get; set; }

    /// <summary>
    /// indicates if the sale is posted to accounting
    /// </summary>
    public bool Billed { get; set; }

    public int XferInventoryId { get; set; }

    /// <summary>
    /// references users.id
    /// </summary>
    public long DistributorId { get; set; }

    public string Notes { get; set; } = null!;

    public DateTime? BillDate { get; set; }

    public string? Pricelevel { get; set; }

    /// <summary>
    /// references drug_templates.selector
    /// </summary>
    public string? Selector { get; set; }

    /// <summary>
    /// 1=sale, 2=purchase, 3=return, 4=transfer, 5=adjustment
    /// </summary>
    public sbyte TransType { get; set; }

    public string? Chargecat { get; set; }

    /// <summary>
    /// fk to list_options.option_id where list_id=pharmacy_supply_type to indicate type of dispensing first order, refil, emergency, partial order, etc
    /// </summary>
    public string? PharmacySupplyType { get; set; }

    public DateTime LastUpdated { get; set; }

    public DateTime DateCreated { get; set; }

    /// <summary>
    /// fk to users.id for user that last updated this entry
    /// </summary>
    public long? UpdatedBy { get; set; }

    /// <summary>
    /// fk to users.id for user that created this entry
    /// </summary>
    public long? CreatedBy { get; set; }
}
