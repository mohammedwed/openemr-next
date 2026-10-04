using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Address
{
    public int Id { get; set; }

    public string? Line1 { get; set; }

    public string? Line2 { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Zip { get; set; }

    public string? PlusFour { get; set; }

    public string? Country { get; set; }

    public int? ForeignId { get; set; }

    /// <summary>
    /// The county or district of the address
    /// </summary>
    public string? District { get; set; }
}
