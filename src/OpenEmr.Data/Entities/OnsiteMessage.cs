using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Portal messages
/// </summary>
public partial class OnsiteMessage
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public string? Message { get; set; }

    public string Ip { get; set; } = null!;

    public DateTime Date { get; set; }

    /// <summary>
    /// who sent id
    /// </summary>
    public string? SenderId { get; set; }

    /// <summary>
    /// who to id array
    /// </summary>
    public string RecipId { get; set; } = null!;
}
