using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

/// <summary>What <c>POST .../soap-notes</c> returns: the new note's id within its encounter (ADR 0073) and the encounter it is on.</summary>
public sealed record SoapNoteRecordedResponse(long Id, Guid EncounterUuid);

/// <summary>One SOAP note as the API returns it. <paramref name="ETag"/> is its content version (ADR 0072), the same value as the header; send it in <c>If-Match</c> to correct the note.</summary>
public sealed record SoapNoteResponse(long Id, DateTime? Date, string? RecordedBy, string? Subjective, string? Objective, string? Assessment, string? Plan, string ETag)
{
    public static SoapNoteResponse From(SoapNoteView v) => new(v.Id, v.Date, v.RecordedBy, v.Subjective, v.Objective, v.Assessment, v.Plan, v.Version().ToETag());
}

/// <summary>One bounded page of an encounter's SOAP notes, newest first.</summary>
public sealed record SoapNoteListResponse(IReadOnlyList<SoapNoteResponse> Items, int Total, int Limit, int Offset);
