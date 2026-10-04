using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class VerifyEmail
{
    public long Id { get; set; }

    public long? PidHolder { get; set; }

    public string? Email { get; set; }

    public string? Language { get; set; }

    public string? Fname { get; set; }

    public string? Mname { get; set; }

    public string? Lname { get; set; }

    public DateOnly? Dob { get; set; }

    public string? TokenOnetime { get; set; }

    public sbyte Active { get; set; }
}
