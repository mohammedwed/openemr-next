using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

/// <summary>What <c>POST .../clinical-notes</c> returns.</summary>
public sealed record ClinicalNoteRecordedResponse(Guid Uuid, Guid EncounterUuid);

/// <summary>One clinical note as the API returns it. <paramref name="ETag"/> is its current version, the same value as the header; send it in <c>If-Match</c> to correct the note.</summary>
public sealed record ClinicalNoteResponse(
    Guid Uuid, DateTime? Date, string? Type, string? Category, string? Description, string? Code, string? CodeText, string? RecordedBy, string? ETag = null)
{
    public static ClinicalNoteResponse From(ClinicalNoteView v) =>
        new(v.Uuid, v.Date, v.Type, v.Category, v.Description, v.Code, v.CodeText, v.RecordedBy, OpenEmrNext.ClinicalCare.Api.Endpoints.Preconditions.ETagOf(v.LastUpdated));
}

/// <summary>One bounded page of an encounter's clinical notes, newest first.</summary>
public sealed record ClinicalNoteListResponse(IReadOnlyList<ClinicalNoteResponse> Items, int Total, int Limit, int Offset);

/// <summary>A note type a caller can choose, as <c>type</c>.</summary>
public sealed record ClinicalNoteTypeLookupResponse(string Id, string Title, string Code);

/// <summary>A note category a caller can choose, as <c>category</c>.</summary>
public sealed record ClinicalNoteCategoryLookupResponse(string Id, string Title);
