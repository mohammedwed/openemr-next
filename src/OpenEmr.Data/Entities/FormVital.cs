using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormVital
{
    public long Id { get; set; }

    public byte[]? Uuid { get; set; }

    public DateTime? Date { get; set; }

    public long? Pid { get; set; }

    public string? User { get; set; }

    public string? Groupname { get; set; }

    public sbyte? Authorized { get; set; }

    public sbyte? Activity { get; set; }

    public string? Bps { get; set; }

    public string? Bpd { get; set; }

    public decimal? Weight { get; set; }

    public decimal? Height { get; set; }

    public decimal? Temperature { get; set; }

    public string? TempMethod { get; set; }

    public decimal? Pulse { get; set; }

    public decimal? Respiration { get; set; }

    public string? Note { get; set; }

    public decimal? Bmi { get; set; }

    public string? BmiStatus { get; set; }

    public decimal? WaistCirc { get; set; }

    public decimal? HeadCirc { get; set; }

    public decimal? OxygenSaturation { get; set; }

    public decimal? OxygenFlowRate { get; set; }

    public string? ExternalId { get; set; }

    public decimal? PedWeightHeight { get; set; }

    public decimal? PedBmi { get; set; }

    public decimal? PedHeadCirc { get; set; }

    public decimal? InhaledOxygenConcentration { get; set; }

    public DateTime LastUpdated { get; set; }
}
