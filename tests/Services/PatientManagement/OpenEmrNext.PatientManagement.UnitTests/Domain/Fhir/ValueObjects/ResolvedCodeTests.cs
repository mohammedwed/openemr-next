using OpenEmrNext.PatientManagement.Domain.Fhir.ValueObjects;

namespace OpenEmrNext.PatientManagement.UnitTests.Domain.Fhir.ValueObjects;

public class ResolvedCodeTests
{
    [Fact]
    public void Create_with_a_valid_system_and_code_succeeds()
    {
        var code = ResolvedCode.Create("http://snomed.info/sct", "248152002", "Male");

        Assert.Equal("http://snomed.info/sct", code.CodeSystem);
        Assert.Equal("248152002", code.CodeValue);
        Assert.Equal("Male", code.DisplayText);
    }

    [Fact]
    public void Create_accepts_no_display()
    {
        var code = ResolvedCode.Create("http://snomed.info/sct", "248152002", null);

        Assert.Null(code.DisplayText);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_rejects_a_blank_system(string? system) =>
        Assert.Throws<ResolvedCodeValidationException>(() => ResolvedCode.Create(system!, "248152002", "Male"));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_rejects_a_blank_code(string? code) =>
        Assert.Throws<ResolvedCodeValidationException>(() => ResolvedCode.Create("http://snomed.info/sct", code!, "Male"));
}
