using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Problems;

/// <summary>The read side: a get is a plain lookup, a list checks its bounds before it asks anything (API-6).</summary>
public class ProblemReadUseCasesTests
{
    private static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");

    private sealed class FakePatients(PatientReference? patient) : IPatientDirectory
    {
        public int Calls { get; private set; }

        public Task<PatientReference?> FindAsync(Guid patientUuid, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(patient is not null && patient.Uuid == patientUuid ? patient : null);
        }
    }

    private sealed class FakeReader(ProblemView? view) : IProblemReader
    {
        public int ListCalls { get; private set; }
        public PageRequest? LastPage { get; private set; }

        public Task<ProblemView?> FindAsync(Guid problemUuid, CancellationToken ct) =>
            Task.FromResult(view is not null && view.Uuid == problemUuid ? view : null);

        public Task<ProblemPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct)
        {
            ListCalls++;
            LastPage = page;
            return Task.FromResult(new ProblemPage(view is null ? [] : [view], view is null ? 0 : 1));
        }
    }

    private static ProblemView View() =>
        new(Guid.NewGuid(), PatientUuid, 42, "Asthma", new DateTime(2020, 5, 1), null, "ICD10:J45.909", 0, 1, null, null, null, null, null);

    [Fact]
    public async Task A_get_returns_the_view_or_null()
    {
        var view = View();
        var useCase = new GetProblemUseCase(new FakeReader(view));

        Assert.Same(view, await useCase.ExecuteAsync(view.Uuid, CancellationToken.None));
        Assert.Null(await useCase.ExecuteAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task A_list_returns_the_patients_page()
    {
        var reader = new FakeReader(View());
        var useCase = new ListPatientProblemsUseCase(new FakePatients(PatientReference.Create(PatientUuid, 42)), reader);

        var page = await useCase.ExecuteAsync(PatientUuid, 10, 5, CancellationToken.None);

        Assert.Equal(1, page.Total);
        Assert.Equal(10, reader.LastPage!.Limit);
        Assert.Equal(5, reader.LastPage.Offset);
    }

    [Fact]
    public async Task A_list_with_bad_bounds_is_rejected_before_the_patient_is_even_looked_up()
    {
        var patients = new FakePatients(PatientReference.Create(PatientUuid, 42));
        var useCase = new ListPatientProblemsUseCase(patients, new FakeReader(null));

        await Assert.ThrowsAsync<ClinicalCareValidationException>(() => useCase.ExecuteAsync(PatientUuid, 10_000, 0, CancellationToken.None));

        Assert.Equal(0, patients.Calls);
    }

    [Fact]
    public async Task A_list_for_an_unknown_patient_is_not_found_and_asks_the_reader_nothing()
    {
        var reader = new FakeReader(null);
        var useCase = new ListPatientProblemsUseCase(new FakePatients(null), reader);

        await Assert.ThrowsAsync<PatientNotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid(), null, null, CancellationToken.None));

        Assert.Equal(0, reader.ListCalls);
    }
}
