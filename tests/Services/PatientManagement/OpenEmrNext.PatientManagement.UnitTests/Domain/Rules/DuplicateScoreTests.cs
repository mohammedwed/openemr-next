using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Domain.Rules;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;

namespace OpenEmrNext.PatientManagement.UnitTests.Domain.Rules;

public class DuplicateScoreTests
{
    [Theory]
    [InlineData(0, DuplicateScoreLevel.None)]
    [InlineData(11, DuplicateScoreLevel.None)]
    [InlineData(12, DuplicateScoreLevel.WorthReviewing)]
    [InlineData(16, DuplicateScoreLevel.WorthReviewing)]
    [InlineData(17, DuplicateScoreLevel.NearCertain)]
    [InlineData(27, DuplicateScoreLevel.NearCertain)]
    public void Classify_matches_legacy_thresholds(int score, DuplicateScoreLevel expected) =>
        Assert.Equal(expected, DuplicateScore.Classify(score));

    [Fact]
    public void Unique_and_pending_sentinels_classify_as_none()
    {
        Assert.Equal(DuplicateScoreLevel.None, DuplicateScore.Classify(DuplicateScore.Unique));
        Assert.Equal(DuplicateScoreLevel.None, DuplicateScore.Classify(DuplicateScore.Pending));
    }
}
