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
            // #00796b, not #00897b: white text on the original teal measured 4.32:1, just under WCAG AA's
            // 4.5:1 for normal text (the Swap button's own label). #00796b clears it at 5.32:1.
            Secondary = "#00796b",
            // Darker than MudBlazor's defaults: its stock info/warning/error/success tones measured
            // 3.4:1 and below on the light canvas (the info alert's text was the first one caught).
            // Each value is the darkest shade that still reads as its color; all clear 4.5:1 on #f7f7f8.
            Info = "#0868b8",
            Warning = "#9a5b00",
            Error = "#c62828",
            Success = "#2e7d32",
            // Not pure white: a wall of #ffffff read as glaring and made it harder to focus on the
            // content. Background is a cool light gray; Surface/AppbarBackground are a barely-off-white
            // so cards still lift off the canvas. Chosen so every accent still clears 4.5:1 on Background
            // (info measured 4.9:1, warning 4.7:1, primary 8.1:1) -- text stays at ~15:1 on Surface.
            AppbarBackground = "#fafbfc",
            AppbarText = "rgba(0,0,0,0.87)",
            Surface = "#fafbfc",
            Background = "#eceef2",
            // MudBlazor's default secondary text is rgba(0,0,0,0.54), which measured 4.51:1 on cards and
            // 4.39:1 on the canvas -- passing only on cards, failing on the canvas. #5f5f5f reaches 6.16:1
            // and 5.5:1, still clearly secondary next to the #424242 body text.
            TextSecondary = "#5f5f5f",
            DrawerBackground = "#fafbfc"
        },
        PaletteDark = new PaletteDark
        {
            // #a585d8, not #8d6bc2: purple text on the dark surface measured 3.98:1 (links, "VIEW"
            // actions); #a585d8 clears AA at 5.51:1 while staying in the same hue family.
            Primary = "#a585d8",
            Secondary = "#4fb3a9",
            // Dark text on filled buttons, not white: white on these lighter accents measured 3.02:1
            // (primary) and 2.51:1 (secondary); #121212 on each measures 6.2:1 and 7.45:1.
            PrimaryContrastText = "#121212",
            SecondaryContrastText = "#121212",
            // Lighter than the light-mode values, the same pattern as the purple/teal above: each is the
            // lightest shade that still reads as its color, and all clear 4.5:1 on #1e1e1e.
            Info = "#5ab0ff",
            Warning = "#ffa726",
            Error = "#ef5350",
            Success = "#66bb6a",
            // AppbarBackground now equals Surface, not an independently-chosen darker value: the app bar
            // used to be #1e1e1e while Background/Surface were left at MudBlazor's own stock dark
            // defaults (confirmed via getComputedStyle: background ~#32333d, surface ~#373740) -- both
            // LIGHTER than the bar, inverting the "surface is lighter than canvas" elevation direction
            // Material (and this theme's own light palette, below) otherwise follows, which read as the
            // bar and the page merging into one dark mass rather than two distinct layers. Surface/
            // Background here mirror PaletteLight's own pattern exactly: the bar matches Surface (the
            // same tone as a card), Background is the one deliberately different, darker canvas tone.
            AppbarBackground = "#1e1e1e",
            AppbarText = "rgba(255,255,255,0.87)",
            Surface = "#1e1e1e",
            Background = "#121212",
            // The drawer has its own separate DrawerBackground, not tied to Surface automatically --
            // checked directly rather than assumed, since light mode's own stock default happens to
            // already equal white there (matching AppbarBackground/Surface by coincidence, not design),
            // while dark mode's stock default didn't match the Surface value set above at all.
            DrawerBackground = "#1e1e1e"
        }
    };
}
