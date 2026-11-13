using Microsoft.EntityFrameworkCore;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Infrastructure;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// Proves the weighted SQL comparison (<see cref="EfDuplicateScorer"/>) actually reproduces legacy
/// OpenEMR's own scoring against a real MariaDB, not just that it compiles.
/// </summary>
/// <remarks>
/// The fixture's database is shared across every test in this collection, not reset between them --
/// so every scored field (email, DOB, sex, name), not just the obvious one, must be made unique per
/// test with a fresh GUID. A test that reuses another test's fixed value (discovered the hard way:
/// an earlier draft hardcoded "ada@example.com" in two different tests) silently matches that other
/// test's leftover row instead of the row it actually means to compare against.
/// </remarks>
[Collection("PatientDataContainer")]
public class DuplicateScoringTests(PatientDataContainerFixture fixture)
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static async Task<Patient> Register(
        PatientManagementDbContext context, string firstName, string lastName, string sex, DateOnly dob, string? email = null)
    {
        var registration = new PatientRegistration(firstName, lastName, sex, dob, email, null);
        return await new RegisterPatientUseCase(new EfPatientRegistry(context))
            .ExecuteAsync(registration, registeredBy: 1, Now, CancellationToken.None);
    }

    private static string UniqueSex() => $"TestSex{Guid.NewGuid():N}"[..30];

    [Fact]
    public async Task A_new_patient_with_no_one_to_compare_against_scores_zero()
    {
        await using var context = fixture.CreateDbContext();
        var patient = await Register(
            context, "Solo", $"Patient{Guid.NewGuid():N}", UniqueSex(), new DateOnly(2075, 5, 5), $"solo{Guid.NewGuid():N}@example.com");

        var score = await new EfDuplicateScorer(context).RescoreAsync(patient.Pid, CancellationToken.None);

        // COALESCE(...,0) in the SQL -- MAX() over zero genuinely-matching candidate rows is NULL,
        // matching legacy's own default-to-0 behavior (DuplicatePatientService::recalculateScore()),
        // not legacy's unrelated dupscore column DEFAULT of -9 (Pending), which only applies before
        // any score is computed. Every field here (sex included) is unique to this test so a leftover
        // row from another test in this shared database can't accidentally contribute a match.
        Assert.Equal(0, score);
        Assert.Equal(DuplicateScoreLevel.None, DuplicateScore.Classify(score));
    }

    [Fact]
    public async Task Two_patients_with_identical_demographics_score_as_near_certain()
    {
        var lastName = $"Lovelace{Guid.NewGuid():N}";
        var sex = UniqueSex();
        var email = $"ada{Guid.NewGuid():N}@example.com";
        var dob = new DateOnly(1990, 1, 1);

        await using var context = fixture.CreateDbContext();
        await Register(context, "Ada", lastName, sex, dob, email);
        var second = await Register(context, "Ada", lastName, sex, dob, email);

        var score = await new EfDuplicateScorer(context).RescoreAsync(second.Pid, CancellationToken.None);

        // email(6) + DOB(6) + exact name(6) + sex(6) + soundex-lastname(2) = 26; no phone set by
        // registration, so the phone component (worth 1) never fires here.
        Assert.Equal(26, score);
        Assert.Equal(DuplicateScoreLevel.NearCertain, DuplicateScore.Classify(score));

        await using var verify = fixture.CreateDbContext();
        var reloaded = await verify.Patients.SingleAsync(p => p.Pid == second.Pid);
        Assert.NotNull(reloaded); // dupscore itself isn't mapped on Patient; the row still round-trips fine.
    }

    [Fact]
    public async Task Two_unrelated_patients_score_zero_against_each_other()
    {
        await using var context = fixture.CreateDbContext();
        await Register(context, "Grace", $"Hopper{Guid.NewGuid():N}", UniqueSex(), new DateOnly(1906, 12, 9), $"grace{Guid.NewGuid():N}@example.com");
        var second = await Register(context, "Alan", $"Turing{Guid.NewGuid():N}", UniqueSex(), new DateOnly(1912, 6, 23), $"alan{Guid.NewGuid():N}@example.com");

        var score = await new EfDuplicateScorer(context).RescoreAsync(second.Pid, CancellationToken.None);

        Assert.Equal(0, score);
        Assert.Equal(DuplicateScoreLevel.None, DuplicateScore.Classify(score));
    }

    [Fact]
    public async Task A_patient_marked_unique_never_contributes_to_anyone_elses_score()
    {
        var lastName = $"Byron{Guid.NewGuid():N}";
        var sex = UniqueSex();
        var email = $"byron{Guid.NewGuid():N}@example.com";
        var dob = new DateOnly(1815, 12, 10);

        await using var context = fixture.CreateDbContext();
        var first = await Register(context, "Ada", lastName, sex, dob, email);
        var second = await Register(context, "Ada", lastName, sex, dob, email);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE patient_data SET dupscore = {DuplicateScore.Unique} WHERE pid = {first.Pid}");

        var score = await new EfDuplicateScorer(context).RescoreAsync(second.Pid, CancellationToken.None);

        Assert.Equal(0, score);
    }
}
