using MudBlazor;

namespace OpenEmrNext.WebApp.Theme;

/// <summary>
/// A deliberately chosen palette, not MudBlazor's own bare defaults. Primary is OpenEMR's own brand
/// purple (<c>#5c2d91</c>, the dominant color already baked into <c>wwwroot/icon-192.png</c> --
/// confirmed by sampling the icon's actual pixels, not guessed), so the dashboard reads as part of the
/// same product, not a generic MudBlazor scaffold. Secondary is a contrasting teal, used sparingly for
/// actions that are deliberately distinct from the primary flow (for example `Register.razor`'s swap
/// action), the same role MudBlazor's own default secondary color would have played.
/// </summary>
public static class OpenEmrNextTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#5c2d91",
            Secondary = "#00897b"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#8d6bc2",
            Secondary = "#4fb3a9"
        }
    };
}
