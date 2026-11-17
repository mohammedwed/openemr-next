using System.Reflection;
using OpenEmrNext.ClinicalCare.Domain;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.ArchitectureTests;

/// <summary>
/// Guards the one thing that keeps an aggregate's or value object's invariant from being bypassed (DOM-2,
/// ARC-10): no public constructor, so the only way in is the type's own <c>Create</c> factory, and no public
/// setter, which also closes the <c>with</c>-expression escape hatch records give for free. A change that
/// quietly adds either "to make testing easier" fails here.
/// </summary>
/// <remarks>
/// <see cref="EncounterClassOption"/>, <see cref="FacilityOption"/>, <see cref="EncounterReferenceData"/>,
/// <see cref="Quantity"/> and <see cref="VitalsInput"/> are
/// deliberately not listed: they mirror rows legacy already holds and carry no invariant of their own, so
/// they are read tolerantly and have plain constructors (DOM-9).
/// </remarks>
public class InvariantEnforcementTests
{
    public static IEnumerable<object[]> InvariantEnforcingTypes =>
    [
        [typeof(Encounter)],
        [typeof(EncounterCreation)],
        [typeof(EncounterChange)],
        [typeof(EncounterIdentity)],
        [typeof(PatientReference)],
        [typeof(PageRequest)],
        [typeof(Vitals)],
        [typeof(VitalsRecording)],
        [typeof(VitalsIdentity)],
        [typeof(EncounterReference)]
    ];

    [Theory]
    [MemberData(nameof(InvariantEnforcingTypes))]
    public void Has_no_public_constructor(Type type)
    {
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Theory]
    [MemberData(nameof(InvariantEnforcingTypes))]
    public void Has_no_publicly_settable_property(Type type)
    {
        var publiclySettable = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(publiclySettable);
    }
}
