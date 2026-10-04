using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class OauthTrustedUser
{
    public long Id { get; set; }

    public string? UserId { get; set; }

    public string? ClientId { get; set; }

    public string? Scope { get; set; }

    public bool? PersistLogin { get; set; }

    public DateTime? Time { get; set; }

    public string? Code { get; set; }

    public string? SessionCache { get; set; }

    public string? GrantType { get; set; }
}
