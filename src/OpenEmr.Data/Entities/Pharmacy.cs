using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Pharmacy
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int TransmitMethod { get; set; }

    public string? Email { get; set; }

    public int? Ncpdp { get; set; }

    public int? Npi { get; set; }
}
