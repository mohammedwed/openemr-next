using OpenEmrNext.PatientManagement.Domain.Fhir;
using OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;
using OpenEmrNext.PatientManagement.Domain.Fhir.Rules;

namespace OpenEmrNext.PatientManagement.UnitTests.Domain.Fhir.Rules;

public class PatientGenderMappingTests
{
    [Theory]
    [InlineData("Male", "male", "M")]
    [InlineData("Female", "female", "F")]
    [InlineData("male", "male", "M")]
    [InlineData("OTH", "other", "OTH")]
    [InlineData("ASKU", "other", "ASKU")]
    [InlineData("asked-declined", "other", "asked-declined")]
    [InlineData("UNK", "unknown", "UNK")]
    [InlineData("", "other", "UNK")]
    [InlineData(null, "other", "UNK")]
    [InlineData("something-else-entirely", "other", "UNK")]
    public void Resolve_matches_legacys_switch(string? sex, string expectedGender, string expectedBirthSex)
    {
        var (gender, birthSexCode) = PatientGenderMapping.Resolve(sex);

        Assert.Equal(expectedGender, gender);
        Assert.Equal(expectedBirthSex, birthSexCode);
    }
}
