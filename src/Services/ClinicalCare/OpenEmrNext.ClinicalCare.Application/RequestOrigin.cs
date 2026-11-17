namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>
/// Where a request came from, as far as the audit trail (SEC-9) records it: a correlation id to join log lines and audit rows, and the
/// caller's network address. Built by the API from the HTTP request so no use case knows about HTTP; both are optional because a
/// command can also come from a test or a future non-HTTP entry point.
/// </summary>
public sealed record RequestOrigin(string? CorrelationId, string? SourceAddress);
