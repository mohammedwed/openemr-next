using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>Reads a facility's name and default place-of-service code (legacy <c>facility</c>; design set R10).</summary>
public interface IFacilityDirectory
{
    /// <returns>The facility, or <c>null</c> when none has this id.</returns>
    Task<FacilityOption?> FindAsync(int facilityId, CancellationToken ct);
}
