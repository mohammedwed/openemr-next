using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Registry
{
    public string? Name { get; set; }

    public sbyte? State { get; set; }

    public string? Directory { get; set; }

    public long Id { get; set; }

    public sbyte? SqlRun { get; set; }

    public sbyte? Unpackaged { get; set; }

    public DateTime? Date { get; set; }

    public int? Priority { get; set; }

    public string? Category { get; set; }

    public string? Nickname { get; set; }

    public sbyte PatientEncounter { get; set; }

    public sbyte TherapyGroupEncounter { get; set; }

    public string AcoSpec { get; set; } = null!;

    /// <summary>
    /// An id to a form repository. Primarily questionnaire_repository.
    /// </summary>
    public long? FormForeignId { get; set; }
}
