using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.ClinicalCare.Composition;

/// <summary>
/// How long Clinical Care keeps its own bookkeeping (finding R-10). Bound from the <c>Retention</c> section. These are the only
/// records anything here ever deletes; encounters and vitals are clinical data and are never purged by this service.
/// </summary>
public sealed class RetentionOptions
{
    /// <summary>The configuration section name, <c>Retention</c>.</summary>
    public const string SectionName = "Retention";

    /// <summary>Whether this host runs the purge.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Days a processed event id is remembered. The id only has to outlast the producer's retries, which span minutes (the relay
    /// dead-letters after about ten), so a month is generous; keeping it longer only grows the table.
    /// </summary>
    [Range(1, 3650)]
    public int ProcessedEventDays { get; set; } = 30;

    /// <summary>Days a delivered outbox row is kept (once something relays Clinical Care's outbox).</summary>
    [Range(1, 3650)]
    public int PublishedOutboxDays { get; set; } = 30;

    /// <summary>
    /// Days an outbox row that was **never** delivered is kept. No consumer of Clinical Care's events exists yet, so these rows are
    /// currently the only record that the events happened, which is why this is long. Each one is derivable from its encounter or
    /// vitals row, so removing the oldest loses a notification, never a clinical fact. Revisit when the first consumer exists.
    /// </summary>
    [Range(1, 3650)]
    public int UnpublishedOutboxDays { get; set; } = 365;

    /// <summary>Hours between runs.</summary>
    [Range(1, 168)]
    public int IntervalHours { get; set; } = 6;

    /// <summary>Rows deleted per statement, per table.</summary>
    [Range(10, 10_000)]
    public int BatchSize { get; set; } = 1000;
}
