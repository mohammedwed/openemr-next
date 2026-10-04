using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormEyeAntseg
{
    /// <summary>
    /// Links to forms.form_id
    /// </summary>
    public long Id { get; set; }

    public long? Pid { get; set; }

    public string? Odschirmer1 { get; set; }

    public string? Osschirmer1 { get; set; }

    public string? Odschirmer2 { get; set; }

    public string? Osschirmer2 { get; set; }

    public string? Odtbut { get; set; }

    public string? Ostbut { get; set; }

    public string? Osconj { get; set; }

    public string? Odconj { get; set; }

    public string? Odcornea { get; set; }

    public string? Oscornea { get; set; }

    public string? Odac { get; set; }

    public string? Osac { get; set; }

    public string? Odlens { get; set; }

    public string? Oslens { get; set; }

    public string? Odiris { get; set; }

    public string? Osiris { get; set; }

    public string? PupilNormal { get; set; }

    public string? Odpupilsize1 { get; set; }

    public string? Odpupilsize2 { get; set; }

    public string? Odpupilreactivity { get; set; }

    public string? Odapd { get; set; }

    public string? Ospupilsize1 { get; set; }

    public string? Ospupilsize2 { get; set; }

    public string? Ospupilreactivity { get; set; }

    public string? Osapd { get; set; }

    public string? Dimodpupilsize1 { get; set; }

    public string? Dimodpupilsize2 { get; set; }

    public string? Dimodpupilreactivity { get; set; }

    public string? Dimospupilsize1 { get; set; }

    public string? Dimospupilsize2 { get; set; }

    public string? Dimospupilreactivity { get; set; }

    public string? PupilComments { get; set; }

    public string? Odkthickness { get; set; }

    public string? Oskthickness { get; set; }

    public string? Odgonio { get; set; }

    public string? Osgonio { get; set; }

    public string? AntsegComments { get; set; }
}
