using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using OpenEmrNext.ClinicalCare.Client;

namespace OpenEmrNext.WebApp.UnitTests;

/// <summary>
/// A component test host for the WebApp: bUnit's context with MudBlazor's real services and its three providers rendered (so a dialog or a snackbar a component opens actually appears in the markup), and the typed
/// clinical care client replaced by a substitute the test scripts. Real components, real dialogs, no browser.
/// </summary>
internal sealed class WebAppTestHost : IAsyncDisposable
{
    public WebAppTestHost()
    {
        Ctx = new BunitContext();
        Ctx.JSInterop.Mode = JSRuntimeMode.Loose;   // MudBlazor calls into JavaScript for layout; none of it matters to what these tests check
        Ctx.Services.AddMudServices();
        Ctx.Services.AddSingleton(ClinicalCare);
        Ctx.Services.AddSingleton<TimeProvider>(Clock);
        Ctx.Services.AddScoped<OpenEmrNext.WebApp.Shared.IdempotencyAttempt>();   // the create dialogs hold one per attempt, as in the app's own Program.cs

        Providers = Ctx.Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
            builder.OpenComponent<MudSnackbarProvider>(2);
            builder.CloseComponent();
        });
    }

    public BunitContext Ctx { get; }

    public IClinicalCareClient ClinicalCare { get; } = Substitute.For<IClinicalCareClient>();

    /// <summary>The clock the dialogs read their default times from, pinned so a test can say what "now" is: 2026-11-18 04:30 UTC.</summary>
    public FixedTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 18, 4, 30, 0, TimeSpan.Zero));

    /// <summary>Everything MudBlazor renders outside a component: open dialogs and snackbars.</summary>
    public IRenderedComponent<IComponent> Providers { get; }

    /// <summary>Clicks the button with this text in the open dialog.</summary>
    public void ClickInDialog(string text)
    {
        Providers.WaitForAssertion(() => Assert.Contains(Providers.FindAll("button"), b => b.TextContent.Trim() == text));
        Providers.FindAll("button").First(b => b.TextContent.Trim() == text).Click();
    }

    public void ShouldShow(string text) => Providers.WaitForAssertion(() => Assert.Contains(text, Providers.Markup));

    public void ShouldNotShow(string text) => Assert.DoesNotContain(text, Providers.Markup);

    public ValueTask DisposeAsync() => Ctx.DisposeAsync();
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
