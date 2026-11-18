using MudBlazor;
using ClinicalCareApiException = OpenEmrNext.ClinicalCare.Client.ApiException;

namespace OpenEmrNext.WebApp.Shared;

/// <summary>
/// The one place a delete is confirmed and its outcomes are put into words (M5g, ADR 0076), so the three panels that offer it say the same thing. Deleting needs legacy's <c>admin/super</c> right, which the session does not
/// show the browser, so Delete is offered and a refusal is explained rather than the button being hidden on a guess.
/// </summary>
public static class RecordDeletion
{
    /// <summary>
    /// Asks, then runs <paramref name="delete"/> and reports. Returns <c>true</c> when the list should be read again: the record was deleted, or it turned out to be gone or changed (so what is on screen was stale).
    /// Returns <c>false</c> when the user cancelled or was refused: nothing changed, the list is as it was. When the service did not answer clearly (a 5xx, or no connection) the outcome is unknown, so that also returns <c>true</c> and the list is read again.
    /// </summary>
    public static async Task<bool> ConfirmAndDeleteAsync(IDialogService dialogs, ISnackbar snackbar, string title, string message, Func<Task> delete)
    {
        var confirmed = await dialogs.ShowMessageBoxAsync(title, message, yesText: "Delete", cancelText: "Keep it");
        if (confirmed != true)
        {
            return false;
        }

        try
        {
            await delete();
            snackbar.Add("Deleted.", Severity.Success);
            return true;
        }
        catch (ClinicalCareApiException ex) when (ex.StatusCode == 403)
        {
            snackbar.Add("Only an administrator can delete records. Nothing was deleted.", Severity.Warning);
        }
        catch (ClinicalCareApiException ex) when (ex.StatusCode == 404)
        {
            snackbar.Add("That record is already gone, so nothing was deleted. The list now shows what is stored.", Severity.Warning);
            return true;
        }
        catch (ClinicalCareApiException ex) when (ex.StatusCode == 412)
        {
            snackbar.Add("That record was changed by someone else, so nothing was deleted. The list now shows what is stored; delete it again if you still want to.", Severity.Warning);
            return true;
        }
        catch (ClinicalCareApiException ex) when (ex.StatusCode >= 500)
        {
            // The service may have finished the delete before the reply failed, so "nothing was deleted" would be a guess. Say what is known and read the list again.
            snackbar.Add("The service did not confirm the delete, so it may or may not have happened. The list now shows what is stored.", Severity.Warning);
            return true;
        }
        catch (ClinicalCareApiException ex)
        {
            snackbar.Add(ClinicalCareErrorFormatter.Describe(ex, "Could not delete this record. Nothing was deleted."), Severity.Error);
        }
        catch (HttpRequestException)
        {
            snackbar.Add("Could not reach the clinical care service, so the delete may or may not have happened. The list now shows what is stored.", Severity.Warning);
            return true;
        }

        return false;
    }
}
