using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Application;

public class EncounterReadUseCaseTests
{
    private static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");

    private sealed class FakePatients(bool exists) : IPatientDirectory
    {
        public Task<PatientReference?> FindAsync(Guid patientUuid, CancellationToken ct) =>
            Task.FromResult<PatientReference?>(exists ? PatientReference.Create(patientUuid, 42) : null);
    }

    private sealed class FakeReader : IEncounterReader
    {
        public int ListCalls { get; private set; }
        public PageRequest? LastPage { get; private set; }

        public Task<EncounterView?> FindAsync(Guid encounterUuid, CancellationToken ct) =>
            Task.FromResult<EncounterView?>(encounterUuid == Guid.Empty ? null : View(encounterUuid));

        public Task<EncounterPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct)
        {
            ListCalls++;
            LastPage = page;
            return Task.FromResult(new EncounterPage([View(Guid.NewGuid())], 1));
        }

        private static EncounterView View(Guid uuid) =>
            new(uuid, 1001, PatientUuid, 42, "AMB", null, null, null, 0, null, null, 7, null);
    }

    [Fact]
    public async Task Get_returns_the_view_the_reader_found()
    {
        var uuid = Guid.NewGuid();

        var view = await new GetEncounterUseCase(new FakeReader()).ExecuteAsync(uuid, CancellationToken.None);

        Assert.Equal(uuid, view!.Uuid);
    }

    [Fact]
    public async Task Get_returns_null_when_there_is_no_such_encounter() =>
        Assert.Null(await new GetEncounterUseCase(new FakeReader()).ExecuteAsync(Guid.Empty, CancellationToken.None));

    [Fact]
    public async Task List_for_an_unknown_patient_is_not_found_and_never_reads()
    {
        var reader = new FakeReader();

        await Assert.ThrowsAsync<PatientNotFoundException>(() =>
            new ListPatientEncountersUseCase(new FakePatients(false), reader).ExecuteAsync(PatientUuid, null, null, CancellationToken.None));

        Assert.Equal(0, reader.ListCalls);
    }

    [Fact]
    public async Task List_rejects_an_oversized_page_before_touching_the_database()
    {
        var reader = new FakeReader();

        await Assert.ThrowsAsync<EncounterValidationException>(() =>
            new ListPatientEncountersUseCase(new FakePatients(true), reader).ExecuteAsync(PatientUuid, 10_000, null, CancellationToken.None));

        Assert.Equal(0, reader.ListCalls);
    }

    [Fact]
    public async Task List_passes_the_bounded_page_to_the_reader()
    {
        var reader = new FakeReader();

        var page = await new ListPatientEncountersUseCase(new FakePatients(true), reader)
            .ExecuteAsync(PatientUuid, 10, 20, CancellationToken.None);

        Assert.Equal(1, page.Total);
        Assert.Equal(10, reader.LastPage!.Limit);
        Assert.Equal(20, reader.LastPage.Offset);
    }
}
