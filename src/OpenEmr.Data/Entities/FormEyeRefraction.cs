using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormEyeRefraction
{
    /// <summary>
    /// Links to forms.form_id
    /// </summary>
    public long Id { get; set; }

    public long? Pid { get; set; }

    public string? Mrodsph { get; set; }

    public string? Mrodcyl { get; set; }

    public string? Mrodaxis { get; set; }

    public string? Mrodprism { get; set; }

    public string? Mrodbase { get; set; }

    public string? Mrodadd { get; set; }

    public string? Mrossph { get; set; }

    public string? Mroscyl { get; set; }

    public string? Mrosaxis { get; set; }

    public string? Mrosprism { get; set; }

    public string? Mrosbase { get; set; }

    public string? Mrosadd { get; set; }

    public string? Mrodnearsphere { get; set; }

    public string? Mrodnearcyl { get; set; }

    public string? Mrodnearaxis { get; set; }

    public string? Mrodprismnear { get; set; }

    public string? Mrodbasenear { get; set; }

    public string? Mrosnearshpere { get; set; }

    public string? Mrosnearcyl { get; set; }

    public string? Mrosnearaxis { get; set; }

    public string? Mrosprismnear { get; set; }

    public string? Mrosbasenear { get; set; }

    public string? Crodsph { get; set; }

    public string? Crodcyl { get; set; }

    public string? Crodaxis { get; set; }

    public string? Crossph { get; set; }

    public string? Croscyl { get; set; }

    public string? Crosaxis { get; set; }

    public string? Crcomments { get; set; }

    public string Balanced { get; set; } = null!;

    public string? Arodsph { get; set; }

    public string? Arodcyl { get; set; }

    public string? Arodaxis { get; set; }

    public string? Arossph { get; set; }

    public string? Aroscyl { get; set; }

    public string? Arosaxis { get; set; }

    public string? Arodadd { get; set; }

    public string? Arosadd { get; set; }

    public string? Arnearodva { get; set; }

    public string? Arnearosva { get; set; }

    public string? Arodprism { get; set; }

    public string? Arosprism { get; set; }

    public string? Ctlodsph { get; set; }

    public string? Ctlodcyl { get; set; }

    public string? Ctlodaxis { get; set; }

    public string? Ctlodbc { get; set; }

    public string? Ctloddiam { get; set; }

    public string? Ctlossph { get; set; }

    public string? Ctloscyl { get; set; }

    public string? Ctlosaxis { get; set; }

    public string? Ctlosbc { get; set; }

    public string? Ctlosdiam { get; set; }

    public string? CtlComments { get; set; }

    public string? Ctlmanufacturerod { get; set; }

    public string? Ctlsupplierod { get; set; }

    public string? Ctlbrandod { get; set; }

    public string? Ctlmanufactureros { get; set; }

    public string? Ctlsupplieros { get; set; }

    public string? Ctlbrandos { get; set; }

    public string? Ctlodadd { get; set; }

    public string? Ctlosadd { get; set; }

    public string? Nvochecked { get; set; }

    public string? Addchecked { get; set; }
}
