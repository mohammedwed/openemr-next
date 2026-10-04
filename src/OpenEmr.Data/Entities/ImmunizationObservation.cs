using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ImmunizationObservation
{
    public int ImoId { get; set; }

    public int ImoImId { get; set; }

    public int? ImoPid { get; set; }

    public string? ImoCriteria { get; set; }

    public string? ImoCriteriaValue { get; set; }

    public int? ImoUser { get; set; }

    public string? ImoCode { get; set; }

    public string? ImoCodetext { get; set; }

    public string? ImoCodetype { get; set; }

    public DateOnly? ImoVisDatePublished { get; set; }

    public DateOnly? ImoVisDatePresented { get; set; }

    public DateTime ImoDateObservation { get; set; }
}
