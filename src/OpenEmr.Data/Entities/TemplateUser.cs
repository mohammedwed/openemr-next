using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class TemplateUser
{
    public int TuId { get; set; }

    public int? TuUserId { get; set; }

    public int? TuFacilityId { get; set; }

    public int? TuTemplateId { get; set; }

    public int? TuTemplateOrder { get; set; }
}
