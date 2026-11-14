using System.Reflection;
using OpenEmrNext.PatientManagement.Domain.Insurance;

namespace OpenEmrNext.PatientManagement.ArchitectureTests;

/// <summary>
/// Guards a specific bug class found in <c>AddInsurancePolicyUseCase</c>: a child entity's own
/// factory, once an aggregate exists whose job is to enforce an invariant across the whole cluster,
/// must not stay publicly callable -- otherwise nothing stops Application code from constructing the
/// entity directly and silently skipping the cluster-wide check (here, <see cref="InsuranceCoverage"/>'s
/// "no two policies at the same level and start date" rule). <see cref="InsurancePolicy.Add"/> was
/// <c>public</c> until this was found; <c>AddInsurancePolicyUseCase</c> called it directly instead of
/// going through <see cref="InsuranceCoverage.AddPolicy"/>, reachable only through the swap path.
/// </summary>
/// <remarks>
/// Each row names the entity factory that must not be public, and the aggregate method that is the
/// only correct way to reach it -- recorded for traceability, not read by the test itself. Add a row
/// here whenever a child entity gains a cluster-aware aggregate wrapper, the same way
/// <c>InvariantEnforcementTests</c> grows a row whenever a new aggregate or value object is added.
/// </remarks>
public class ClusterInvariantEnforcementTests
{
    public static IEnumerable<object[]> ChildEntityFactoriesWithAnAggregateAlternative =>
    [
        [typeof(InsurancePolicy), "Add", typeof(InsuranceCoverage), nameof(InsuranceCoverage.AddPolicy)]
    ];

    [Theory]
    [MemberData(nameof(ChildEntityFactoriesWithAnAggregateAlternative))]
    public void Child_entity_factory_is_not_publicly_callable_once_an_aggregate_alternative_exists(
        Type entityType, string factoryMethodName, Type aggregateType, string aggregateMethodName)
    {
        var publicFactory = entityType.GetMethod(factoryMethodName, BindingFlags.Public | BindingFlags.Static);

        Assert.Null(publicFactory);
        // aggregateType/aggregateMethodName aren't asserted on directly -- they document which
        // aggregate method is the correct replacement, so a failure here points straight at it.
        Assert.NotNull(aggregateType.GetMethod(aggregateMethodName, BindingFlags.Public | BindingFlags.Instance));
    }
}
