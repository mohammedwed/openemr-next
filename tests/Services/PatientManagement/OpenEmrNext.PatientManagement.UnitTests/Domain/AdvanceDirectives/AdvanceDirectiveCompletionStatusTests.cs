using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;

namespace OpenEmrNext.PatientManagement.UnitTests.Domain.AdvanceDirectives;

public class AdvanceDirectiveCompletionStatusTests
{
    [Theory]
    [InlineData(AdvanceDirectiveCompletionStatus.Yes, "YES")]
    [InlineData(AdvanceDirectiveCompletionStatus.No, "NO")]
    [InlineData(AdvanceDirectiveCompletionStatus.Unknown, "")]
    public void ToStoredValue_and_FromStoredValue_round_trip(AdvanceDirectiveCompletionStatus status, string storedValue)
    {
        Assert.Equal(storedValue, status.ToStoredValue());
        Assert.Equal(status, AdvanceDirectiveCompletionStatusExtensions.FromStoredValue(storedValue));
    }

    [Fact]
    public void FromStoredValue_treats_anything_else_as_unknown() =>
        Assert.Equal(AdvanceDirectiveCompletionStatus.Unknown, AdvanceDirectiveCompletionStatusExtensions.FromStoredValue("garbage"));

    [Theory]
    [InlineData(AdvanceDirectiveCompletionStatus.Yes, "yes")]
    [InlineData(AdvanceDirectiveCompletionStatus.No, "no")]
    [InlineData(AdvanceDirectiveCompletionStatus.Unknown, "unknown")]
    public void ToWireValue_and_TryParseWireValue_round_trip(AdvanceDirectiveCompletionStatus status, string wireValue)
    {
        Assert.Equal(wireValue, status.ToWireValue());
        Assert.Equal(status, AdvanceDirectiveCompletionStatusExtensions.TryParseWireValue(wireValue));
    }

    [Fact]
    public void TryParseWireValue_rejects_an_unknown_value() =>
        Assert.Null(AdvanceDirectiveCompletionStatusExtensions.TryParseWireValue("maybe"));
}
