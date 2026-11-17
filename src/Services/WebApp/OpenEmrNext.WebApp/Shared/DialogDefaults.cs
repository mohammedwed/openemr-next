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

    /// <summary>The same style, one size wider, for forms with enough fields that two columns fit (vitals).</summary>
    public static DialogOptions Wide => new()
    {
        MaxWidth = MaxWidth.Medium,
        FullWidth = true,
        CloseOnEscapeKey = true,
    };
}
