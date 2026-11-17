using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Task = System.Threading.Tasks.Task;

namespace OpenEmrNext.ClinicalCare.Api.Fhir;

/// <summary>A FHIR resource as an HTTP answer: <c>application/fhir+json</c>, an optional weak <c>ETag</c>, and the status (ADR 0079).</summary>
internal sealed class FhirResult(Resource body, int statusCode = StatusCodes.Status200OK, string? weakETag = null) : IResult
{
    public const string ContentType = "application/fhir+json";
    private static readonly FhirJsonSerializer Serializer = new();

    public async Task ExecuteAsync(HttpContext http)
    {
        http.Response.StatusCode = statusCode;
        http.Response.ContentType = ContentType;
        if (weakETag is not null)
            http.Response.Headers.ETag = weakETag;

        await http.Response.WriteAsync(Serializer.SerializeToString(body));
    }
}

/// <summary>The <c>OperationOutcome</c>s a FHIR client is told when it asks for something that is not there, or asks wrongly.</summary>
internal static class FhirOutcomes
{
    public static IResult NotFound(string resourceType, Guid id) =>
        Error(StatusCodes.Status404NotFound, OperationOutcome.IssueType.NotFound, $"{resourceType}/{id} does not exist.");

    /// <summary>A request the service will not guess at: an unsupported parameter, a malformed value, a missing required one. Always 400, never a silently different answer (ADR 0079 #6).</summary>
    public static IResult Invalid(string diagnostics) => Error(StatusCodes.Status400BadRequest, OperationOutcome.IssueType.Invalid, diagnostics);

    private static IResult Error(int status, OperationOutcome.IssueType type, string diagnostics) =>
        new FhirResult(
            new OperationOutcome
            {
                Issue = [new OperationOutcome.IssueComponent { Severity = OperationOutcome.IssueSeverity.Error, Code = type, Diagnostics = diagnostics }]
            },
            status);
}
