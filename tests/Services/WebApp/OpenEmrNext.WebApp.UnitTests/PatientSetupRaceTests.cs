using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using OpenEmrNext.ClinicalCare.Client;
using OpenEmrNext.WebApp.Pages.Patients;

namespace OpenEmrNext.WebApp.UnitTests;

/// <summary>
/// A patient registered a moment ago is not yet known to Clinical Care: it learns of them from the registration event, a few seconds later, and
/// answers 404 until then. The patient page asks straight away, so for a few seconds every Clinical Care section failed with "the clinical care
/// service may be unavailable". That is not an outage, so those sections now say the patient is still being set up, and check again by themselves.
/// </summary>
public class PatientSetupRaceTests
{
    private static readonly Guid Patient = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");

    private static ApiException Status(int status) => new("status", status, "", new Dictionary<string, IEnumerable<string>>(), null!);

    [Fact]
    public async Task A_404_for_a_new_patient_says_still_being_set_up_not_that_the_service_is_unavailable()
    {
        await using var host = new WebAppTestHost();
        host.ClinicalCare.ListEncountersAsync(Arg.Any<Guid?>(), Arg.Any<int?>(), Arg.Any<int?>())
            .Returns(_ => Task.FromException<EncounterListResponse>(Status(404)));

        var panel = host.Ctx.Render<PatientEncountersPanel>(p => p.Add(x => x.PatientUuid, Patient));

        panel.WaitForAssertion(() => Assert.Contains("still being set up", panel.Markup));
        Assert.DoesNotContain("may be unavailable", panel.Markup);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    public async Task Any_other_failure_still_says_the_service_may_be_unavailable(int status)
    {
        await using var host = new WebAppTestHost();
        host.ClinicalCare.ListEncountersAsync(Arg.Any<Guid?>(), Arg.Any<int?>(), Arg.Any<int?>())
            .Returns(_ => Task.FromException<EncounterListResponse>(Status(status)));

        var panel = host.Ctx.Render<PatientEncountersPanel>(p => p.Add(x => x.PatientUuid, Patient));

        panel.WaitForAssertion(() => Assert.Contains("may be unavailable", panel.Markup));
        Assert.DoesNotContain("still being set up", panel.Markup);
    }

    [Fact]
    public async Task The_section_appears_by_itself_once_clinical_care_has_learned_of_the_patient()
    {
        await using var host = new WebAppTestHost();
        var calls = 0;
        host.ClinicalCare.ListEncountersAsync(Arg.Any<Guid?>(), Arg.Any<int?>(), Arg.Any<int?>()).Returns(_ =>
            ++calls == 1
                ? Task.FromException<EncounterListResponse>(Status(404))
                : Task.FromResult(new EncounterListResponse { Items = [], Total = 0, Limit = 10 }));

        // The panel's own retry delay is the card's default of three seconds, so this one waits for it once.
        var panel = host.Ctx.Render<PatientEncountersPanel>(p => p.Add(x => x.PatientUuid, Patient));
        panel.WaitForAssertion(() => Assert.Contains("still being set up", panel.Markup));

        panel.WaitForAssertion(() => Assert.Contains("No encounters recorded", panel.Markup), TimeSpan.FromSeconds(10));
        Assert.DoesNotContain("still being set up", panel.Markup);
        Assert.Equal(2, calls);
    }

    /// <summary>Polls, because a retry changes no markup: bUnit's own waits re-check only when the component renders.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }

    private static IRenderedComponent<PatientSectionCard> Card(WebAppTestHost host, bool settingUp, Action onRetry) =>
        host.Ctx.Render<PatientSectionCard>(p => p
            .Add(x => x.Title, "Encounters")
            .Add(x => x.HasData, false)
            .Add(x => x.Error, true)
            .Add(x => x.StillSettingUp, settingUp)
            .Add(x => x.RetryDelay, TimeSpan.FromMilliseconds(40))
            .Add(x => x.OnRetry, EventCallback.Factory.Create(host, onRetry)));

    [Fact]
    public async Task While_a_patient_is_still_being_set_up_the_card_retries_by_itself_but_only_so_many_times()
    {
        await using var host = new WebAppTestHost();
        var retries = 0;
        var card = Card(host, settingUp: true, () => Interlocked.Increment(ref retries));

        // A real parent re-renders the card after each failed reload, which is what schedules the next retry.
        for (var i = 1; i <= PatientSectionCard.MaxAutomaticRetries; i++)
        {
            await WaitUntilAsync(() => Volatile.Read(ref retries) >= i);
            Assert.Equal(i, Volatile.Read(ref retries));
            card.Render(p => p.Add(x => x.StillSettingUp, true));
        }

        card.Render(p => p.Add(x => x.StillSettingUp, true));
        await Task.Delay(300);   // several retry delays: a sixth retry would have fired by now
        Assert.Equal(PatientSectionCard.MaxAutomaticRetries, Volatile.Read(ref retries));
    }

    [Fact]
    public async Task A_plain_failure_is_never_retried_automatically()
    {
        await using var host = new WebAppTestHost();
        var retries = 0;
        var card = Card(host, settingUp: false, () => Interlocked.Increment(ref retries));

        await Task.Delay(300);

        Assert.Equal(0, Volatile.Read(ref retries));
        Assert.Contains("Could not load", card.Markup);
    }
}
