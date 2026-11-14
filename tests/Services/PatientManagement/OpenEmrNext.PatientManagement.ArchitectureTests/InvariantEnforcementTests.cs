using System.Reflection;
using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ReadModels;
using OpenEmrNext.PatientManagement.Domain.Fhir.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Insurance;
using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Insurance.Rules;
using OpenEmrNext.PatientManagement.Domain.Insurance.ReadModels;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;

namespace OpenEmrNext.PatientManagement.ArchitectureTests;

/// <summary>
/// Guards the one thing that actually keeps every aggregate's/value object's invariant from being
/// bypassed: no public constructor (the only way in is the type's own
/// <c>Register</c>/<c>Add</c>/<c>Upload</c>/<c>Create</c> factory) and no publicly settable property --
/// the second one closes the <c>with</c>-expression escape hatch records offer for free. Confirmed by
/// hand, not just asserted: <c>someResolvedCode with { Code = "" }</c> is a compile error (CS0200)
/// specifically because <see cref="Domain.Fhir.ValueObjects.ResolvedCode.Code"/> has no
/// <c>init</c>/<c>set</c> -- <c>with { }</c> (an untouched copy) still compiles, but nothing settable
/// means nothing mutable. A future change that quietly adds a public setter or constructor "to make
/// testing easier" fails here instead of silently reopening either gap.
/// </summary>
public class InvariantEnforcementTests
{
    public static IEnumerable<object[]> InvariantEnforcingTypes =>
    [
        [typeof(Patient)],
        [typeof(InsurancePolicy)],
        [typeof(AdvanceDirectiveDocument)],
        [typeof(PatientRegistration)],
        [typeof(ResolvedCode)]
    ];

    [Theory]
    [MemberData(nameof(InvariantEnforcingTypes))]
    public void Has_no_public_constructor(Type type)
    {
        var publicConstructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Empty(publicConstructors);
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
