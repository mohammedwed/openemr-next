using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormEyeMagWearing
{
    public int Id { get; set; }

    public int Encounter { get; set; }

    public short FormId { get; set; }

    public long Pid { get; set; }

    public int RxNumber { get; set; }

    public string? Odsph { get; set; }

    public string? Odcyl { get; set; }

    public string? Odaxis { get; set; }

    public string? Ossph { get; set; }

    public string? Oscyl { get; set; }

    public string? Osaxis { get; set; }

    public string? Odmidadd { get; set; }

    public string? Osmidadd { get; set; }

    public string? Odadd { get; set; }

    public string? Osadd { get; set; }

    public string? Odva { get; set; }

    public string? Osva { get; set; }

    public string? Odnearva { get; set; }

    public string? Osnearva { get; set; }

    public string? Odhpd { get; set; }

    public string? Odhbase { get; set; }

    public string? Odvpd { get; set; }

    public string? Odvbase { get; set; }

    public string? Odslaboff { get; set; }

    public string? Odvertexdist { get; set; }

    public string? Oshpd { get; set; }

    public string? Oshbase { get; set; }

    public string? Osvpd { get; set; }

    public string? Osvbase { get; set; }

    public string? Osslaboff { get; set; }

    public string? Osvertexdist { get; set; }

    public string? Odmpdd { get; set; }

    public string? Odmpdn { get; set; }

    public string? Osmpdd { get; set; }

    public string? Osmpdn { get; set; }

    public string? Bpdd { get; set; }

    public string? Bpdn { get; set; }

    public string? LensMaterial { get; set; }

    public string? LensTreatments { get; set; }

    public string? RxType { get; set; }

    public string? Comments { get; set; }
}
