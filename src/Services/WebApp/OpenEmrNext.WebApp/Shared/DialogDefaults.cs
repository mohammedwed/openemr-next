using MudBlazor;

namespace OpenEmrNext.WebApp.Shared;

/// <summary>The one dialog style the patient pages use, so every change dialog behaves the same way.</summary>
public static class DialogDefaults
{
    public static DialogOptions Standard => new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseOnEscapeKey = true,
    };
}
