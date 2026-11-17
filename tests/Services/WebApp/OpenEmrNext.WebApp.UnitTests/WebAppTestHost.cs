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
