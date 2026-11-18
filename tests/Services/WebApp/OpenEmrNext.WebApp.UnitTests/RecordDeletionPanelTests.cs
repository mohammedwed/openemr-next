using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OpenEmrNext.ClinicalCare.Client;
using OpenEmrNext.WebApp.Pages.Encounters;
using OpenEmrNext.WebApp.Pages.Patients;

namespace OpenEmrNext.WebApp.UnitTests;

/// <summary>
/// Deleting from the three encounter panels (M5g, ADR 0076), through the real components, MudBlazor's real confirmation dialog and snackbar, and a scripted client: what the user is asked, what is sent (the row's own
/// <c>ETag</c>), and what they are told for each answer the service can give. The WebApp had no automated tests before these.
/// </summary>
public class RecordDeletionPanelTests
{
    public enum Panel { Vitals, Soap, ClinicalNote, Problem, Allergy }

    private static readonly Guid Encounter = Guid.Parse("e0e0e0e0-0000-4000-8000-000000000001");
    private static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");
    private static readonly Guid RowUuid = Guid.Parse("d0d0d0d0-0000-4000-8000-000000000001");
    private const string RowETag = "\"row-version-1\"";

    /// <summary>One panel, rendered with one row, and the three questions a test asks of it.</summary>
    private sealed class Arranged(WebAppTestHost host, Func<IReadOnlyList<IElement>> buttons, Action<Action> waitFor, Action<Exception> deleteFails, Func<int> deletesSent, Func<int> listsRead, Action verifyDeleteArguments)
    {
        public WebAppTestHost Host { get; } = host;
        public IReadOnlyList<IElement> Buttons => buttons();
        public void WaitFor(Action assertion) => waitFor(assertion);
        public Action<Exception> DeleteFails { get; } = deleteFails;
        public int DeletesSent => deletesSent();
        public int ListsRead => listsRead();
        public void VerifyDeleteArguments() => verifyDeleteArguments();

        public void ClickRowDelete()
        {
            WaitFor(() => Assert.Contains(Buttons, b => b.TextContent.Trim() == "Delete"));
            Buttons.First(b => b.TextContent.Trim() == "Delete").Click();
        }
    }

    private static Arranged Arrange(Panel kind)
    {
        var host = new WebAppTestHost();
        var client = host.ClinicalCare;

        switch (kind)
        {
            case Panel.Vitals:
                client.ListVitalsAsync(Encounter, 10, 0, Arg.Any<string>()).Returns(_ => Task.FromResult(new VitalsListResponse
                {
                    Items = [new VitalsResponse { Uuid = RowUuid, RecordedAt = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero), ETag = RowETag }],
                    Total = 1, Limit = 10
                }));
                return Build(
                    host, c => c.Render<EncounterVitalsPanel>(p => p.Add(x => x.EncounterUuid, Encounter)), ex => client.DeleteVitalsAsync(Encounter, RowUuid, RowETag).ThrowsAsync(ex),
                    () => client.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IClinicalCareClient.DeleteVitalsAsync)),
                    () => client.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IClinicalCareClient.ListVitalsAsync)),
                    () => client.Received(1).DeleteVitalsAsync(Encounter, RowUuid, RowETag));
            case Panel.Soap:
                client.ListSoapNotesAsync(Encounter, 10, 0).Returns(_ => Task.FromResult(new SoapNoteListResponse
                {
                    Items = [new SoapNoteResponse { Id = 88, Date = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero), Subjective = "cough", ETag = RowETag }],
                    Total = 1, Limit = 10
                }));
                return Build(
                    host, c => c.Render<EncounterSoapNotesPanel>(p => p.Add(x => x.EncounterUuid, Encounter)), ex => client.DeleteSoapNoteAsync(Encounter, 88, RowETag).ThrowsAsync(ex),
                    () => client.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IClinicalCareClient.DeleteSoapNoteAsync)),
                    () => client.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IClinicalCareClient.ListSoapNotesAsync)),
                    () => client.Received(1).DeleteSoapNoteAsync(Encounter, 88, RowETag));
            case Panel.ClinicalNote:
                client.ListClinicalNotesAsync(Encounter, 10, 0).Returns(_ => Task.FromResult(new ClinicalNoteListResponse
                {
                    Items = [new ClinicalNoteResponse { Uuid = RowUuid, Date = new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero), Description = "comfortable", ETag = RowETag }],
                    Total = 1, Limit = 10
                }));
                return Build(
                    host, c => c.Render<EncounterClinicalNotesPanel>(p => p.Add(x => x.EncounterUuid, Encounter)), ex => client.DeleteClinicalNoteAsync(Encounter, RowUuid, RowETag).ThrowsAsync(ex),
                    () => client.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IClinicalCareClient.DeleteClinicalNoteAsync)),
                    () => client.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IClinicalCareClient.ListClinicalNotesAsync)),
                    () => client.Received(1).DeleteClinicalNoteAsync(Encounter, RowUuid, RowETag));
            case Panel.Problem:
                client.ListProblemsAsync(PatientUuid, 10, 0).Returns(_ => Task.FromResult(new ProblemListResponse
                {
                    Items = [new ProblemResponse { Uuid = RowUuid, Title = "Asthma", ETag = RowETag }],
                    Total = 1, Limit = 10
                }));
                return Build(
                    host, c => c.Render<PatientProblemsPanel>(p => p.Add(x => x.PatientUuid, PatientUuid)), ex => client.DeleteProblemAsync(RowUuid, RowETag).ThrowsAsync(ex),
                    () => client.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IClinicalCareClient.DeleteProblemAsync)),
                    () => client.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IClinicalCareClient.ListProblemsAsync)),
                    () => client.Received(1).DeleteProblemAsync(RowUuid, RowETag));
            default:
                client.ListAllergiesAsync(PatientUuid, 10, 0).Returns(_ => Task.FromResult(new AllergyListResponse
                {
                    Items = [new AllergyResponse { Uuid = RowUuid, Title = "Penicillin", ETag = RowETag }],
                    Total = 1, Limit = 10
                }));
                return Build(
                    host, c => c.Render<PatientAllergiesPanel>(p => p.Add(x => x.PatientUuid, PatientUuid)), ex => client.DeleteAllergyAsync(RowUuid, RowETag).ThrowsAsync(ex),
                    () => client.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IClinicalCareClient.DeleteAllergyAsync)),
                    () => client.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IClinicalCareClient.ListAllergiesAsync)),
                    () => client.Received(1).DeleteAllergyAsync(RowUuid, RowETag));
        }
    }

    private static Arranged Build<TPanel>(WebAppTestHost host, Func<BunitContext, IRenderedComponent<TPanel>> render, Action<Exception> deleteFails, Func<int> deletes, Func<int> lists, Action verify)
        where TPanel : IComponent
    {
        var panel = render(host.Ctx);
        panel.WaitForAssertion(() => Assert.Contains(panel.FindAll("button"), b => b.TextContent.Trim() == "Delete"));   // the row has loaded
        return new Arranged(host, () => panel.FindAll("button"), a => panel.WaitForAssertion(a), deleteFails, deletes, lists, verify);
    }

    private static ApiException Refused(int status) => new("refused", status, "", new Dictionary<string, IEnumerable<string>>(), null!);

    [Theory]
    [InlineData(Panel.Vitals)]
    [InlineData(Panel.Soap)]
    [InlineData(Panel.ClinicalNote)]
    [InlineData(Panel.Problem)]
    [InlineData(Panel.Allergy)]
    public async Task Every_row_offers_delete_named_for_the_record(Panel kind)
    {
        await using var a = new Disposable(Arrange(kind));

        Assert.Contains(a.Value.Buttons, b => b.TextContent.Trim() == "Delete" && (b.GetAttribute("aria-label") ?? "").StartsWith("Delete "));
    }

    [Theory]
    [InlineData(Panel.Vitals)]
    [InlineData(Panel.Soap)]
    [InlineData(Panel.ClinicalNote)]
    [InlineData(Panel.Problem)]
    [InlineData(Panel.Allergy)]
    public async Task Nothing_is_sent_until_the_user_confirms_and_cancelling_sends_nothing(Panel kind)
    {
        await using var a = new Disposable(Arrange(kind));

        a.Value.ClickRowDelete();
        a.Value.Host.ShouldShow("Keep it");
        Assert.Equal(0, a.Value.DeletesSent);   // asked, not yet done

        a.Value.Host.ClickInDialog("Keep it");

        Assert.Equal(0, a.Value.DeletesSent);
        Assert.Equal(1, a.Value.ListsRead);   // not reloaded either
    }

    [Theory]
    [InlineData(Panel.Vitals)]
    [InlineData(Panel.Soap)]
    [InlineData(Panel.ClinicalNote)]
    [InlineData(Panel.Problem)]
    [InlineData(Panel.Allergy)]
    public async Task Confirming_sends_the_rows_own_version_and_reloads_the_list(Panel kind)
    {
        await using var a = new Disposable(Arrange(kind));

        a.Value.ClickRowDelete();
        a.Value.Host.ClickInDialog("Delete");

        a.Value.Host.ShouldShow("Deleted.");
        a.Value.VerifyDeleteArguments();   // the encounter, the record, and the ETag the row was listed with
        a.Value.WaitFor(() => Assert.Equal(2, a.Value.ListsRead));
    }

    [Theory]
    [InlineData(Panel.Vitals)]
    [InlineData(Panel.Soap)]
    [InlineData(Panel.ClinicalNote)]
    [InlineData(Panel.Problem)]
    [InlineData(Panel.Allergy)]
    public async Task Someone_who_may_not_delete_is_told_so_and_the_list_is_left_alone(Panel kind)
    {
        await using var a = new Disposable(Arrange(kind));
        a.Value.DeleteFails(Refused(403));

        a.Value.ClickRowDelete();
        a.Value.Host.ClickInDialog("Delete");

        a.Value.Host.ShouldShow("Only an administrator can delete records");
        a.Value.Host.ShouldNotShow("Deleted.");
        Assert.Equal(1, a.Value.ListsRead);
    }

    [Theory]
    [InlineData(Panel.Vitals, 412, "changed by someone else")]
    [InlineData(Panel.Soap, 412, "changed by someone else")]
    [InlineData(Panel.ClinicalNote, 412, "changed by someone else")]
    [InlineData(Panel.Problem, 412, "changed by someone else")]
    [InlineData(Panel.Allergy, 412, "changed by someone else")]
    [InlineData(Panel.Vitals, 404, "already gone")]
    [InlineData(Panel.Soap, 404, "already gone")]
    [InlineData(Panel.ClinicalNote, 404, "already gone")]
    [InlineData(Panel.Problem, 404, "already gone")]
    [InlineData(Panel.Allergy, 404, "already gone")]
    public async Task A_record_that_changed_or_vanished_says_so_and_the_list_is_read_again(Panel kind, int status, string words)
    {
        await using var a = new Disposable(Arrange(kind));
        a.Value.DeleteFails(Refused(status));

        a.Value.ClickRowDelete();
        a.Value.Host.ClickInDialog("Delete");

        a.Value.Host.ShouldShow(words);
        a.Value.Host.ShouldShow("nothing was deleted");
        a.Value.WaitFor(() => Assert.Equal(2, a.Value.ListsRead));
    }

    [Theory]
    [InlineData(Panel.Vitals)]
    [InlineData(Panel.Soap)]
    [InlineData(Panel.ClinicalNote)]
    [InlineData(Panel.Problem)]
    [InlineData(Panel.Allergy)]
    public async Task A_network_failure_does_not_claim_the_delete_did_not_happen_and_the_list_is_read_again(Panel kind)
    {
        // The request may have reached the service and committed before the connection failed, so "nothing was deleted" would be a guess.
        await using var a = new Disposable(Arrange(kind));
        a.Value.DeleteFails(new HttpRequestException("down"));

        a.Value.ClickRowDelete();
        a.Value.Host.ClickInDialog("Delete");

        a.Value.Host.ShouldShow("may or may not have happened");
        a.Value.Host.ShouldNotShow("Nothing was deleted");
        a.Value.WaitFor(() => Assert.Equal(2, a.Value.ListsRead));
    }

    [Theory]
    [InlineData(Panel.Vitals, 500)]
    [InlineData(Panel.Soap, 502)]
    [InlineData(Panel.ClinicalNote, 503)]
    [InlineData(Panel.Problem, 503)]
    [InlineData(Panel.Allergy, 504)]
    public async Task A_server_error_does_not_claim_nothing_was_deleted_and_the_list_is_read_again(Panel kind, int status)
    {
        await using var a = new Disposable(Arrange(kind));
        a.Value.DeleteFails(Refused(status));

        a.Value.ClickRowDelete();
        a.Value.Host.ClickInDialog("Delete");

        a.Value.Host.ShouldShow("did not confirm the delete");
        a.Value.Host.ShouldNotShow("Nothing was deleted");
        a.Value.WaitFor(() => Assert.Equal(2, a.Value.ListsRead));
    }

    [Fact]
    public async Task The_vitals_confirmation_does_not_promise_that_openemrs_dashboard_stops_showing_them()
    {
        // OpenEMR's dashboard card joins forms to form_vitals on form_id alone, so a deleted set can still appear there as the latest vitals (found in the legacy-UI check).
        await using var a = new Disposable(Arrange(Panel.Vitals));

        a.Value.ClickRowDelete();

        a.Value.Host.ShouldShow("patient dashboard can still show them");
        a.Value.Host.ShouldNotShow("no longer shown here or in OpenEMR");
    }

    [Theory]
    [InlineData(Panel.Vitals)]
    [InlineData(Panel.Soap)]
    [InlineData(Panel.ClinicalNote)]
    public async Task The_confirmation_says_the_record_is_marked_not_removed(Panel kind)
    {
        await using var a = new Disposable(Arrange(kind));

        a.Value.ClickRowDelete();

        a.Value.Host.ShouldShow("not removed from the database");
        a.Value.Host.ShouldShow("can restore");
    }

    [Theory]
    [InlineData(Panel.Problem)]
    [InlineData(Panel.Allergy)]
    public async Task The_confirmation_for_an_issue_says_it_is_removed_and_cannot_be_restored(Panel kind)
    {
        await using var a = new Disposable(Arrange(kind));

        a.Value.ClickRowDelete();

        a.Value.Host.ShouldShow("cannot be restored");
        a.Value.Host.ShouldShow("together with its links to encounters");
    }

    private sealed class Disposable(Arranged value) : IAsyncDisposable
    {
        public Arranged Value { get; } = value;
        public ValueTask DisposeAsync() => Value.Host.DisposeAsync();
    }
}
