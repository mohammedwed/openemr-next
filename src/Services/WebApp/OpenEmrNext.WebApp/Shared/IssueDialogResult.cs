namespace OpenEmrNext.WebApp.Shared;

/// <summary>How an issue (problem or allergy) dialog ended, for the panel that opened it to act on.</summary>
/// <param name="Stale">
/// The save was refused because the record changed after it was read (412). Nothing was saved, and the panel should reload so the user sees what is stored now.
/// </param>
/// <param name="Title">The title of the record that was saved, for the confirmation message; <c>null</c> when <paramref name="Stale"/>.</param>
/// <param name="IsNew">Whether the save recorded a new issue rather than changing one.</param>
public sealed record IssueDialogResult(bool Stale, string? Title, bool IsNew)
{
    public static IssueDialogResult Saved(string title, bool isNew) => new(false, title, isNew);

    public static IssueDialogResult OutOfDate() => new(true, null, false);
}
