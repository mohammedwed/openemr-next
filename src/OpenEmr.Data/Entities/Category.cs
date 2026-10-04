using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Category
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? Value { get; set; }

    public int Parent { get; set; }

    public int Lft { get; set; }

    public int Rght { get; set; }

    public string AcoSpec { get; set; } = null!;

    /// <summary>
    /// Category codes for documents stored in this category
    /// </summary>
    public string Codes { get; set; } = null!;
}
