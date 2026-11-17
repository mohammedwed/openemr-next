using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ClinicalNotes;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>Allocates a time-ordered version 7 uuid for a new clinical note (DOM-7).</summary>
public sealed class GuidClinicalNoteIdentityAllocator : IClinicalNoteIdentityAllocator
{
    public ClinicalNoteIdentity Allocate() => ClinicalNoteIdentity.Create(Guid.CreateVersion7());
}
