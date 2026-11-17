using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>The configured choices an immunization form offers, with their labels (M5d). Validation accepts any configured option; this offers the active ones, as legacy's own form does.</summary>
public interface IImmunizationLookups
{
    Task<ImmunizationOptions> GetOptionsAsync(CancellationToken ct);
}
