using System.Net;
using OpenEmrNext.ClinicalCare.Client;

namespace OpenEmrNext.WebApp.UnitTests;

/// <summary>
/// The five delete calls through the real generated <see cref="ClinicalCareClient"/>, not a substitute for it. The service answers a
/// successful delete with 204 No Content, and the client is generated from the OpenAPI document: if the document does not declare that
/// status the client throws on a delete that worked, and the screen then says it failed. The panel tests script the client, so they
/// cannot see this; these are the tests that do.
/// </summary>
public class GeneratedClientDeleteTests
{
    private sealed class Answering(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Seen { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private static (ClinicalCareClient Client, Answering Handler) Client(HttpStatusCode status)
    {
        var handler = new Answering(status);
        return (new ClinicalCareClient(new HttpClient(handler) { BaseAddress = new Uri("https://localhost") }), handler);
    }

    public static TheoryData<string, Func<ClinicalCareClient, Task>> Deletes => new()
    {
        { "vitals", c => c.DeleteVitalsAsync(Guid.NewGuid(), Guid.NewGuid(), "\"v\"") },
        { "SOAP note", c => c.DeleteSoapNoteAsync(Guid.NewGuid(), 7, "\"c-v\"") },
        { "clinical note", c => c.DeleteClinicalNoteAsync(Guid.NewGuid(), Guid.NewGuid(), "\"v\"") },
        { "problem", c => c.DeleteProblemAsync(Guid.NewGuid(), "\"v\"") },
        { "allergy", c => c.DeleteAllergyAsync(Guid.NewGuid(), "\"v\"") },
    };

    [Theory]
    [MemberData(nameof(Deletes))]
    public async Task A_delete_the_service_answers_with_204_is_a_success_not_an_error(string what, Func<ClinicalCareClient, Task> delete)
    {
        var (client, handler) = Client(HttpStatusCode.NoContent);

        await delete(client);   // throws ApiException ("unexpected status 204") if the document does not declare the 204

        Assert.Equal(HttpMethod.Delete, handler.Seen!.Method);
        Assert.True(handler.Seen.Headers.Contains("If-Match"), $"the {what} delete must carry the ETag it was read with");
    }

    [Theory]
    [MemberData(nameof(Deletes))]
    public async Task A_refusal_still_surfaces_with_its_status_so_the_screen_can_explain_it(string what, Func<ClinicalCareClient, Task> delete)
    {
        var (client, _) = Client(HttpStatusCode.Forbidden);

        var thrown = await Assert.ThrowsAsync<ApiException>(() => delete(client));

        Assert.Equal(403, thrown.StatusCode);
        Assert.False(string.IsNullOrEmpty(what));
    }
}
