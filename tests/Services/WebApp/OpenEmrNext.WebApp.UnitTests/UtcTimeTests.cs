using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using OpenEmrNext.ClinicalCare.Client;
using OpenEmrNext.WebApp.Pages.Patients;
using OpenEmrNext.WebApp.Shared;

namespace OpenEmrNext.WebApp.UnitTests;

/// <summary>
/// Times in the WebApp are UTC (<see cref="UtcTime"/>): what a user enters is what is stored, and what they see afterwards. Before this, the
/// encounter dialog proposed the browser's local time and sent it with the browser's offset, the service stored the UTC equivalent, and the page
/// showed that, so a user in UTC+2 who entered 06:30 saw 04:30.
/// </summary>
public class UtcTimeTests
{
    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public void A_picked_time_is_sent_with_the_same_digits_and_a_zero_offset_whatever_its_kind(DateTimeKind kind)
    {
        var picked = new DateTime(2026, 11, 18, 6, 30, 0, kind);

        var wire = UtcTime.ToWire(picked);

        Assert.Equal(TimeSpan.Zero, wire.Offset);
        Assert.Equal(new DateTime(2026, 11, 18, 6, 30, 0), wire.DateTime);
    }

    [Fact]
    public void A_date_with_no_time_is_midnight_at_a_zero_offset_so_it_cannot_slip_a_day()
    {
        var wire = UtcTime.ToWire(new DateTime(1980, 1, 2));

        Assert.Equal(TimeSpan.Zero, wire.Offset);
        Assert.Equal(new DateOnly(1980, 1, 2), DateOnly.FromDateTime(wire.UtcDateTime));
    }

    [Fact]
    public void A_time_on_a_page_says_it_is_utc_and_is_not_converted()
    {
        Assert.Equal("2026-11-18 04:30 UTC", Formatting.DateTimeText(new DateTimeOffset(2026, 11, 18, 4, 30, 0, TimeSpan.Zero)));
        Assert.Equal("—", Formatting.DateTimeText(null));
    }

    [Fact]
    public void A_date_on_a_page_carries_no_zone_label()
    {
        Assert.Equal("2026-11-18", Formatting.DateText(new DateTimeOffset(2026, 11, 18, 4, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task The_encounter_dialog_proposes_the_utc_time_and_sends_it_as_utc()
    {
        await using var host = new WebAppTestHost();
        var patient = Guid.NewGuid();
        CreateEncounterRequest? sent = null;
        host.ClinicalCare.ListEncounterClassesAsync().Returns(Task.FromResult<ICollection<EncounterClassLookupResponse>>(
            [new EncounterClassLookupResponse { Code = "AMB", Title = "Outpatient", IsDefault = true }]));
        host.ClinicalCare.ListCalendarCategoriesAsync().Returns(Task.FromResult<ICollection<CalendarCategoryLookupResponse>>(
            [new CalendarCategoryLookupResponse { Id = 5, Name = "Office Visit" }]));
        host.ClinicalCare.ListFacilitiesAsync().Returns(Task.FromResult<ICollection<FacilityLookupResponse>>(
            [new FacilityLookupResponse { Id = 3, Name = "Clinic" }]));
        host.ClinicalCare.ListProvidersAsync().Returns(Task.FromResult<ICollection<ProviderLookupResponse>>(
            [new ProviderLookupResponse { Id = 1, DisplayName = "Administrator" }]));
        host.ClinicalCare.CreateEncounterAsync(Arg.Any<CreateEncounterRequest>()).Returns(call =>
        {
            sent = call.Arg<CreateEncounterRequest>();
            return Task.FromResult(new EncounterCreatedResponse { Uuid = Guid.NewGuid(), Number = 1 });
        });

        var dialogs = host.Ctx.Services.GetRequiredService<IDialogService>();
        await host.Providers.InvokeAsync(() => dialogs.ShowAsync<CreateEncounterDialog>("New encounter",
            new DialogParameters<CreateEncounterDialog> { { d => d.PatientUuid, patient } }));

        host.ShouldShow("Time (UTC)");
        // The pinned clock says 04:30 UTC: that, not the machine's local time, is what the dialog proposes.
        host.Providers.WaitForAssertion(() => Assert.Contains(host.Providers.FindAll("input"), i => i.GetAttribute("value") == "04:30"));

        host.ClickInDialog("Create encounter");
        host.Providers.WaitForAssertion(() => Assert.NotNull(sent));

        Assert.Equal(new DateTimeOffset(2026, 11, 18, 4, 30, 0, TimeSpan.Zero), sent!.Date);
        Assert.Equal(TimeSpan.Zero, sent.Date!.Value.Offset);
    }
}
