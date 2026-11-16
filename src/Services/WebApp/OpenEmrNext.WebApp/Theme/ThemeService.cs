using Microsoft.JSInterop;

namespace OpenEmrNext.WebApp.Theme;

/// <summary>
/// Shared between App.razor (hosts MudThemeProvider) and MainLayout.razor (hosts the toggle button) --
/// mutating a plain property on this service doesn't by itself re-render App.razor's component tree, so
/// <see cref="Changed"/> exists purely so App.razor can re-render itself when MainLayout's button flips
/// this from the outside.
/// </summary>
public sealed class ThemeService(IJSRuntime js)
{
    private const string StorageKey = "op-dark-mode";

    public bool IsDarkMode { get; private set; }

    /// <summary>
    /// Null until <see cref="LoadAsync"/> resolves. Once true, the user has made an explicit choice,
    /// which should stop MudThemeProvider from also tracking the OS/browser preference live -- otherwise
    /// a later system theme change would silently override what the user just picked in this app.
    /// </summary>
    public bool? HasExplicitPreference { get; private set; }

    public event Action? Changed;

    public async Task LoadAsync()
    {
        var stored = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        HasExplicitPreference = stored is "dark" or "light";
        IsDarkMode = stored == "dark";
    }

    public async Task SetDarkModeAsync(bool isDarkMode)
    {
        IsDarkMode = isDarkMode;
        HasExplicitPreference = true;
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, isDarkMode ? "dark" : "light");
        Changed?.Invoke();
    }

    /// <summary>
    /// MudThemeProvider's own ObserveSystemDarkModeChange, while no explicit preference is stored yet,
    /// resolves the initial IsDarkMode itself and raises IsDarkModeChanged -- wired back here so a
    /// first-load system-preference match is reflected in the toggle button's own icon too, not just the
    /// theme itself.
    /// </summary>
    public void SetFromSystemPreference(bool isDarkMode)
    {
        IsDarkMode = isDarkMode;
        Changed?.Invoke();
    }
}
