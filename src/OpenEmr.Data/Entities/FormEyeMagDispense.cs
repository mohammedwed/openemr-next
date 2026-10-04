using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormEyeMagDispense
{
    public long Id { get; set; }

    public DateTime Date { get; set; }

    public long? Encounter { get; set; }

    public long? Pid { get; set; }

    public string? User { get; set; }

    public string? Groupname { get; set; }

    public sbyte? Authorized { get; set; }

    public sbyte? Activity { get; set; }

    public DateTime? Refdate { get; set; }

    public string? Reftype { get; set; }

    public string? Rxtype { get; set; }

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

    public string? Ctlmanufacturerod { get; set; }

    public string? Ctlmanufactureros { get; set; }

    public string? Ctlsupplierod { get; set; }

    public string? Ctlsupplieros { get; set; }

    public string? Ctlbrandod { get; set; }

    public string? Ctlbrandos { get; set; }

    public string? Ctlodquantity { get; set; }

    public string? Ctlosquantity { get; set; }

    public string? Oddiam { get; set; }

    public string? Odbc { get; set; }

    public string? Osdiam { get; set; }

    public string? Osbc { get; set; }

    public string? Rxcomments { get; set; }

    public string? Comments { get; set; }
}
