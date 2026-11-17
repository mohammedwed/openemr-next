using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>An audit log that always fails, to prove a change cannot commit without its audit row (SEC-9, ADR 0069 #3).</summary>
internal sealed class ThrowingAuditLog : IAuditLog
{
    public Task AppendAsync(AuditEntry entry, CancellationToken ct) =>
        throw new InvalidOperationException("simulated audit failure");
}
