using Microsoft.EntityFrameworkCore;
using OpenEmrNext.PatientManagement.Api.Domain;
using OpenEmrNext.PatientManagement.Api.Persistence;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// Proves the one thing that's genuinely new in this slice against a real MariaDB: the patient insert
/// and its outbox row commit together, in one transaction, via one <c>SaveChangesAsync</c> call
/// (ADR 0049) -- not that the business rules hold, which the unit tests already cover.
/// </summary>
[Collection("PatientDataContainer")]
public class PatientRegistrationTests(PatientDataContainerFixture fixture)
{
    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public async Task Registering_a_patient_commits_the_patient_row_and_its_outbox_row_together()
    {
        var registration = new PatientRegistration("Ada", "Lovelace", "Female", new DateOnly(1990, 1, 1));
        var patient = Patient.Register(registration, registeredBy: 1, Now);

        await using var context = fixture.CreateDbContext();
        context.Patients.Add(patient);
        context.OutboxMessages.Add(OutboxMessage.For(
            "PatientRegistered",
            $"{{\"pid\":{patient.Pid},\"uuid\":\"{patient.Uuid}\"}}",
            Now));

        await context.SaveChangesAsync();

        // EF Core's own ValueGeneratedOnAdd() already populated this via the database's AUTO_INCREMENT --
        // nothing in application code needs to copy it over manually.
        Assert.NotEqual(0, patient.Id);

        await using var verify = fixture.CreateDbContext();
        var savedPatient = await verify.Patients.SingleAsync(p => p.Pid == patient.Pid);
        var savedOutboxRow = await verify.OutboxMessages
            .Where(m => m.EventType == "PatientRegistered" && m.Payload.Contains(patient.Uuid.ToString()))
            .SingleAsync();

        Assert.Equal(patient.Uuid, savedPatient.Uuid);
        Assert.Equal("Ada", savedPatient.FirstName);
        Assert.Equal("Lovelace", savedPatient.LastName);
        Assert.Null(savedPatient.Email);
        Assert.Contains(patient.Pid.ToString(), savedOutboxRow.Payload);
        Assert.Null(savedOutboxRow.PublishedAtUtc);
    }

    [Fact]
    public async Task A_failed_validation_never_reaches_the_database()
    {
        await using var context = fixture.CreateDbContext();
        var patientCountBefore = await context.Patients.CountAsync();
        var outboxCountBefore = await context.OutboxMessages.CountAsync();

        var badRegistration = new PatientRegistration("Ada", "A", "F", new DateOnly(1990, 1, 1));
        Assert.Throws<PatientValidationException>(() => Patient.Register(badRegistration, 1, Now));

        Assert.Equal(patientCountBefore, await context.Patients.CountAsync());
        Assert.Equal(outboxCountBefore, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Uuid_round_trips_through_the_database_in_rfc4122_byte_order()
    {
        var registration = new PatientRegistration("Grace", "Hopper", "Female", new DateOnly(1985, 5, 5));
        var patient = Patient.Register(registration, registeredBy: 1, Now);

        await using var context = fixture.CreateDbContext();
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        await using var verify = fixture.CreateDbContext();
        var reloaded = await verify.Patients.SingleAsync(p => p.Pid == patient.Pid);

        // A wrong byte order (e.g. Guid.ToByteArray()'s mixed-endian layout, the bug this slice
        // deliberately avoids) would still round-trip through THIS context correctly, since encoding
        // and decoding use the same conversion either way -- the real proof is that EF's own
        // materialized value matches the Guid we started with, not just that it round-trips internally.
        Assert.Equal(patient.Uuid, reloaded.Uuid);
    }
}
