using MudBlazor;

namespace OpenEmrNext.WebApp.Theme;

/// <summary>
/// A deliberately chosen palette, not MudBlazor's own bare defaults. Primary is OpenEMR's own brand
/// purple (<c>#5c2d91</c>, the dominant color already baked into <c>wwwroot/icon-192.png</c> --
/// confirmed by sampling the icon's actual pixels, not guessed), so the dashboard reads as part of the
/// same product, not a generic MudBlazor scaffold. Secondary is a contrasting teal, used sparingly for
/// actions that are deliberately distinct from the primary flow (for example `Register.razor`'s swap
/// action), the same role MudBlazor's own default secondary color would have played.
///
/// AppbarBackground/AppbarText are overridden to a light, near-neutral surface: MudBlazor's own
/// default paints the whole app bar with Primary, which read as too saturated/branded against the
/// rest of the UI. Purple now shows up only as an accent (active nav underline, primary buttons),
/// not as a solid block covering the entire top bar.
/// </summary>
public static class OpenEmrNextTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#5c2d91",
            Secondary = "#00897b",
            AppbarBackground = "#ffffff",
            AppbarText = "rgba(0,0,0,0.87)",
            // A faint tint, not white: Background and Surface (MudPaper/MudTable's own white) were
            // identical, so a card's only separation from the page was a barely-visible 1px shadow --
            // confirmed by inspecting the live computed styles. It read as unstyled, not flat-by-design.
            Background = "#f7f7f8"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#8d6bc2",
            Secondary = "#4fb3a9",
            AppbarBackground = "#1e1e1e",
            AppbarText = "rgba(255,255,255,0.87)"
        }
    };
}
