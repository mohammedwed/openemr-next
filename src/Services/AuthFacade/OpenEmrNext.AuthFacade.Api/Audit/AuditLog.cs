namespace OpenEmrNext.AuthFacade.Api.Audit;

/// <summary>
/// Marker type that fixes the audit trail's logger category name
/// (<c>OpenEmrNext.AuthFacade.Api.Audit.AuditLog</c>), so it stays stable and filterable regardless of
/// which file an <see cref="AuditEvents"/> call happens to live in. Never constructed.
/// </summary>
internal sealed class AuditLog;
