using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using OpenEmrNext.ClinicalCare.Client;
using PmClient = OpenEmrNext.PatientManagement.Client;
using OpenEmrNext.WebApp.Resilience;
using OpenEmrNext.WebApp.Shared;

namespace OpenEmrNext.WebApp.UnitTests;

/// <summary>
/// The WebApp's side of the <c>Idempotency-Key</c> (ADR 0077): a retried save sends the same key, an edited or a new one a different key, a successful save ends the attempt, and nothing but a creating POST carries
/// a key. The last test drives the real generated client through the handler, which is the actual retry scenario.
/// </summary>
public class IdempotencyKeyHandlerTests
{
    /// <summary>The service's own rule for a key (Application.Ports/Idempotency.cs): 1 to 64 letters, digits and _ . : -.</summary>
    private static readonly Regex ServiceKeyRule = new("^[A-Za-z0-9_.:-]{1,64}$");

    private sealed class Recorder(params HttpStatusCode[] answers) : HttpMessageHandler
    {
        private int _next;
        public List<(HttpMethod Method, string Path, string? Key, string Body)> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Sent.Add((request.Method, request.RequestUri!.PathAndQuery, request.Headers.TryGetValues("Idempotency-Key", out var v) ? v.Single() : null, body));
            var status = answers[Math.Min(_next++, answers.Length - 1)];
            return new HttpResponseMessage(status) { Content = new StringContent("""{"uuid":"d0d0d0d0-0000-4000-8000-000000000001"}""", Encoding.UTF8, "application/json") };
        }
    }

    private static HttpClient Client(IdempotencyAttempt attempt, Recorder inner) =>
        new(new IdempotencyKeyHandler(attempt) { InnerHandler = inner }) { BaseAddress = new Uri("https://example.test") };

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    [Fact]
    public async Task A_post_during_an_attempt_carries_a_key_the_service_accepts()
    {
        var attempt = new IdempotencyAttempt();
        attempt.BeginIfNone();
        var inner = new Recorder(HttpStatusCode.Created);

        await Client(attempt, inner).PostAsync("/api/v1/encounters", Json("{\"a\":1}"));

        var key = Assert.Single(inner.Sent).Key;
        Assert.NotNull(key);
        Assert.Matches(ServiceKeyRule, key);
        Assert.StartsWith(attempt.Current ?? "", key);   // not yet cleared when read below; see the next tests for clearing
    }

    [Fact]
    public async Task The_same_attempt_sending_the_same_request_again_sends_the_same_key()
    {
        var attempt = new IdempotencyAttempt();
        attempt.BeginIfNone();
        var inner = new Recorder(HttpStatusCode.InternalServerError);   // the first answer is lost or failed, so the attempt stays
        var client = Client(attempt, inner);

        await client.PostAsync("/api/v1/encounters", Json("{\"a\":1}"));
        await client.PostAsync("/api/v1/encounters", Json("{\"a\":1}"));

        Assert.Equal(2, inner.Sent.Count);
        Assert.Equal(inner.Sent[0].Key, inner.Sent[1].Key);
    }

    [Fact]
    public async Task An_edited_request_in_the_same_attempt_sends_a_different_key()
    {
        var attempt = new IdempotencyAttempt();
        attempt.BeginIfNone();
        var inner = new Recorder(HttpStatusCode.InternalServerError);
        var client = Client(attempt, inner);

        await client.PostAsync("/api/v1/encounters", Json("{\"a\":1}"));
        await client.PostAsync("/api/v1/encounters", Json("{\"a\":2}"));

        Assert.NotEqual(inner.Sent[0].Key, inner.Sent[1].Key);   // otherwise the service would answer 422: the key was used for a different request
    }

    [Fact]
    public async Task The_same_body_to_another_route_sends_a_different_key()
    {
        var attempt = new IdempotencyAttempt();
        attempt.BeginIfNone();
        var inner = new Recorder(HttpStatusCode.InternalServerError);
        var client = Client(attempt, inner);

        await client.PostAsync("/api/v1/encounters/a/vitals", Json("{}"));
        await client.PostAsync("/api/v1/encounters/b/vitals", Json("{}"));

        Assert.NotEqual(inner.Sent[0].Key, inner.Sent[1].Key);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task A_successful_save_ends_the_attempt_so_the_next_save_is_a_new_request(HttpStatusCode success)
    {
        var attempt = new IdempotencyAttempt();
        attempt.BeginIfNone();
        var first = attempt.Current;
        var inner = new Recorder(success);
        var client = Client(attempt, inner);

        await client.PostAsync("/api/v1/encounters", Json("{\"a\":1}"));

        Assert.Null(attempt.Current);
        await client.PostAsync("/api/v1/encounters", Json("{\"a\":1}"));
        Assert.Null(inner.Sent[1].Key);   // no attempt, no key: recording the same thing twice on purpose is two records
        Assert.NotNull(first);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task A_save_that_did_not_succeed_keeps_the_attempt_for_the_retry(HttpStatusCode failure)
    {
        var attempt = new IdempotencyAttempt();
        attempt.BeginIfNone();
        var id = attempt.Current;

        await Client(attempt, new Recorder(failure)).PostAsync("/api/v1/encounters", Json("{}"));

        Assert.Equal(id, attempt.Current);
    }

    [Fact]
    public async Task Nothing_is_added_without_an_attempt_to_a_read_or_to_a_request_that_already_has_a_key()
    {
        var attempt = new IdempotencyAttempt();
        var inner = new Recorder(HttpStatusCode.OK);
        var client = Client(attempt, inner);

        await client.PostAsync("/api/v1/encounters", Json("{}"));   // a POST with no attempt
        attempt.BeginIfNone();
        await client.GetAsync("/api/v1/lookups/facilities");        // a read, during an attempt
        var own = new HttpRequestMessage(HttpMethod.Post, "/api/v1/encounters") { Content = Json("{}") };
        own.Headers.TryAddWithoutValidation("Idempotency-Key", "caller-chose-this");
        await client.SendAsync(own);                                // a caller's own key is kept

        Assert.Null(inner.Sent[0].Key);
        Assert.Null(inner.Sent[1].Key);
        Assert.Equal("caller-chose-this", inner.Sent[2].Key);
    }

    [Fact]
    public async Task Beginning_an_attempt_twice_keeps_the_first_so_a_retry_is_not_a_new_attempt()
    {
        var attempt = new IdempotencyAttempt();
        attempt.BeginIfNone();
        var id = attempt.Current;

        attempt.BeginIfNone();

        Assert.Equal(id, attempt.Current);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task The_generated_client_retrying_a_failed_save_sends_the_same_key_and_a_later_new_save_does_not()
    {
        var attempt = new IdempotencyAttempt();
        var inner = new Recorder(HttpStatusCode.ServiceUnavailable, HttpStatusCode.Created, HttpStatusCode.Created);
        var client = new ClinicalCareClient(Client(attempt, inner));
        var request = new RecordSoapNoteRequest { Subjective = "cough", ProviderId = null };
        var encounter = Guid.Parse("e0e0e0e0-0000-4000-8000-000000000001");

        attempt.BeginIfNone();
        await Assert.ThrowsAsync<ApiException>(() => client.RecordSoapNoteAsync(encounter, request));   // the first try fails
        attempt.BeginIfNone();                                                                         // the dialog's retry keeps the attempt
        await client.RecordSoapNoteAsync(encounter, request);                                          // and succeeds
        await client.RecordSoapNoteAsync(encounter, request);                                          // a later, separate save: no attempt

        Assert.Equal(inner.Sent[0].Key, inner.Sent[1].Key);
        Assert.NotNull(inner.Sent[0].Key);
        Assert.Null(inner.Sent[2].Key);
        Assert.All(inner.Sent, s => Assert.Equal($"/api/v1/encounters/{encounter}/soap-notes", s.Path));
    }

    [Fact]
    public async Task A_directive_document_upload_carries_no_key_even_while_an_attempt_is_open()
    {
        // Patient Management refuses a key on the upload (its file is too large to fingerprint, ADR 0084), and an attempt that outlived a failed dialog must not tag it.
        var attempt = new IdempotencyAttempt();
        attempt.BeginIfNone();
        var inner = new Recorder(HttpStatusCode.Created);

        await Client(attempt, inner).PostAsync("/api/v1/patients/42/advance-directives/documents", Json("{\"fileName\":\"x.pdf\"}"));

        Assert.Null(Assert.Single(inner.Sent).Key);
        Assert.NotNull(attempt.Current);   // the attempt is not this request's, so it is left as it was
    }

    [Fact]
    public async Task The_generated_patient_client_retrying_a_failed_registration_sends_the_same_key()
    {
        var attempt = new IdempotencyAttempt();
        var inner = new Recorder(HttpStatusCode.ServiceUnavailable, HttpStatusCode.Created);
        var client = new PmClient.PatientManagementClient(Client(attempt, inner));
        var request = new PmClient.RegisterPatientRequest { FirstName = "Ada", LastName = "Lovelace", Sex = "female", DateOfBirth = new DateTimeOffset(1815, 12, 10, 0, 0, 0, TimeSpan.Zero) };

        attempt.BeginIfNone();
        await Assert.ThrowsAsync<PmClient.ApiException>(() => client.RegisterPatientAsync(request));   // the first try fails
        attempt.BeginIfNone();                                                                         // the dialog's retry keeps the attempt
        await client.RegisterPatientAsync(request);

        Assert.Equal(2, inner.Sent.Count);
        Assert.NotNull(inner.Sent[0].Key);
        Assert.Equal(inner.Sent[0].Key, inner.Sent[1].Key);
        Assert.Matches(ServiceKeyRule, inner.Sent[0].Key!);
        Assert.All(inner.Sent, s => Assert.Equal("/api/v1/patients", s.Path));
    }
}
