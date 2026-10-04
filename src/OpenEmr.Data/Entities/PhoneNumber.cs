using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class PhoneNumber
{
    public int Id { get; set; }

    public string? CountryCode { get; set; }

    public string? AreaCode { get; set; }

    public string? Prefix { get; set; }

    public string? Number { get; set; }

    public int? Type { get; set; }

    public int? ForeignId { get; set; }
}
